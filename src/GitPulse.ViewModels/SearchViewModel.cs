using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// GitHub typed Search and Search Inbox chrome. Paging uses <see cref="PagedGitHubSession"/>.
/// </summary>
public sealed partial class SearchViewModel : IDisposable
{
    private const int MinimumQueryLength = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public const string SearchHub = "Search";
    public const string ReviewRequestedHub = "Review requested";
    public const string ReviewRequestedQuery =
        "is:open is:pr review-requested:@me archived:false";
    public const string AssignedHub = "Assigned";
    public const string AssignedQuery =
        "is:open assignee:@me archived:false";
    public const string MentionsHub = "Mentions";
    public const string MentionsQuery =
        "is:open mentions:@me archived:false";

    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];
    private readonly Dictionary<SearchType, SearchSession> _sessions = [];
    private readonly SearchInbox _reviewInbox;
    private readonly SearchInbox _assignedInbox;
    private readonly SearchInbox _mentionsInbox;

    private CancellationTokenSource? _requestCts;
    private int _requestVersion;

    public ObservableCollection<Repo> Repositories { get; } = [];
    public ObservableCollection<SearchIssueItem> Issues { get; } = [];
    public ObservableCollection<SearchIssueItem> PullRequests { get; } = [];
    public ObservableCollection<SearchIssueItem> ReviewRequested { get; } = [];
    public ObservableCollection<SearchIssueItem> Assigned { get; } = [];
    public ObservableCollection<SearchIssueItem> Mentions { get; } = [];
    public ObservableCollection<CodeSearchItem> CodeResults { get; } = [];

    public BindableReactiveProperty<string> Query { get; } = new(string.Empty);
    public BindableReactiveProperty<SearchType> SelectedType { get; } = new(SearchType.Repositories);
    public BindableReactiveProperty<string> SelectedHub { get; } = new(SearchHub);
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<bool> CanLoadMore { get; } = new(false);
    public BindableReactiveProperty<bool> HasSearched { get; } = new(false);
    public BindableReactiveProperty<bool> IsEmpty { get; } = new(false);
    public BindableReactiveProperty<int> TotalCount { get; } = new(0);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    public BindableReactiveProperty<bool> IsSearchHub { get; } = new(true);
    public BindableReactiveProperty<bool> IsReviewRequestedHub { get; } = new(false);
    public BindableReactiveProperty<bool> IsAssignedHub { get; } = new(false);
    public BindableReactiveProperty<bool> IsMentionsHub { get; } = new(false);
    public BindableReactiveProperty<bool> IsRepositoriesSelected { get; } = new(true);
    public BindableReactiveProperty<bool> IsIssuesSelected { get; } = new(false);
    public BindableReactiveProperty<bool> IsPullRequestsSelected { get; } = new(false);
    public BindableReactiveProperty<bool> IsCodeSelected { get; } = new(false);
    public BindableReactiveProperty<bool> IsReviewRequestedSelected { get; } = new(false);
    public BindableReactiveProperty<bool> IsAssignedSelected { get; } = new(false);
    public BindableReactiveProperty<bool> IsMentionsSelected { get; } = new(false);

    public SearchViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
        _reviewInbox = new SearchInbox(
            ReviewRequestedQuery, SearchInboxKind.PullRequests, ReviewRequested);
        _assignedInbox = new SearchInbox(
            AssignedQuery, SearchInboxKind.Issues, Assigned);
        _mentionsInbox = new SearchInbox(
            MentionsQuery, SearchInboxKind.Issues, Mentions);

        foreach (var type in Enum.GetValues<SearchType>())
            _sessions[type] = new SearchSession();

        SelectedType
            .DistinctUntilChanged()
            .Subscribe(OnSelectedTypeChanged)
            .AddTo(_disposables);
    }

    [RelayCommand]
    private void SelectType(SearchType type)
    {
        if (IsAnyInboxHub)
            ApplyHub(SearchHub);

        SelectedType.Value = type;
    }

    [RelayCommand]
    private async Task SelectHubAsync(string? hub)
    {
        var next = string.IsNullOrWhiteSpace(hub) ? SearchHub : hub;
        var alreadyOnHub = string.Equals(SelectedHub.Value, next, StringComparison.Ordinal);
        if (alreadyOnHub && HubSessionReady(next))
            return;

        ApplyHub(next);

        if (string.Equals(next, ReviewRequestedHub, StringComparison.Ordinal))
            await LoadInboxAsync(_reviewInbox);
        else if (string.Equals(next, AssignedHub, StringComparison.Ordinal))
            await LoadInboxAsync(_assignedInbox);
        else if (string.Equals(next, MentionsHub, StringComparison.Ordinal))
            await LoadInboxAsync(_mentionsInbox);
        else
            RefreshSelectedState();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SearchAsync()
    {
        if (IsAnyInboxHub)
            ApplyHub(SearchHub);

        var query = Query.Value.Trim();
        if (query.Length < MinimumQueryLength)
        {
            CancelActiveRequest();
            IsLoading.Value = false;
            ErrorMessage.Value = "Enter at least 3 characters to search.";
            return;
        }

        var type = SelectedType.Value;
        var session = _sessions[type];
        session.DisposePaged();

        var (version, requestCts) = BeginRequest();
        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var paged = await StartPagedAsync(session, query, version, requestCts.Token);
            if (paged is null)
                return;

            var api = RestService.For<IGitHubSearchApi>(paged.Client);
            await SearchPageAsync(
                api, type, query, session, replace: true, version, requestCts.Token);

            if (!IsCurrent(version))
                return;

            session.HasSearched = true;
            RefreshSelectedState();
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "Request timed out.";
        }
        catch (SearchRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub Search rate limit exceeded. Wait before trying again.";
        }
        catch (SearchRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub rejected the search query. Check its syntax and qualifiers.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub Search rate limit exceeded. Wait before trying again.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub rejected the search query. Check its syntax and qualifiers.";
        }
        catch (Exception ex)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = $"Search failed: {ex.Message}";
        }
        finally
        {
            CompleteRequest(version, requestCts);
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsReviewRequestedHub.Value)
        {
            await LoadMoreInboxAsync(_reviewInbox);
            return;
        }

        if (IsAssignedHub.Value)
        {
            await LoadMoreInboxAsync(_assignedInbox);
            return;
        }

        if (IsMentionsHub.Value)
        {
            await LoadMoreInboxAsync(_mentionsInbox);
            return;
        }

        var type = SelectedType.Value;
        var session = _sessions[type];
        if (session.Paged is null
            || !session.HasNextPage
            || IsLoading.Value)
        {
            return;
        }

        var (version, requestCts) = BeginRequest();
        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            if (!session.Paged.Advance())
                return;

            session.Paged.PrepareRequest();
            var api = RestService.For<IGitHubSearchApi>(session.Paged.Client);
            await SearchPageAsync(
                api, type, session.Query, session, replace: false, version, requestCts.Token);

            if (!IsCurrent(version))
                return;

            RefreshSelectedState();
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "Request timed out.";
        }
        catch (SearchRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub Search rate limit exceeded. Wait before trying again.";
        }
        catch (SearchRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub rejected the search query. Check its syntax and qualifiers.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub Search rate limit exceeded. Wait before trying again.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "GitHub rejected the search query. Check its syntax and qualifiers.";
        }
        catch (Exception ex)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = $"Load more failed: {ex.Message}";
        }
        finally
        {
            CompleteRequest(version, requestCts);
        }
    }

    private async Task SearchPageAsync(
        IGitHubSearchApi api,
        SearchType type,
        string query,
        SearchSession session,
        bool replace,
        int version,
        CancellationToken cancellationToken)
    {
        switch (type)
        {
            case SearchType.Repositories:
                {
                    var response = await api.SearchRepositories(EncodeQuery(query)).FirstAsync(cancellationToken);
                    if (!IsCurrent(version))
                        return;
                    EnsureSearchSucceeded(response);
                    UpdateCollection(Repositories, response.Content?.Items, replace);
                    UpdateSession(session, response.Content, response.Headers);
                    break;
                }
            case SearchType.Issues:
                {
                    var response = await api.SearchIssues(EncodeQuery($"{query} is:issue"))
                        .FirstAsync(cancellationToken);
                    if (!IsCurrent(version))
                        return;
                    EnsureSearchSucceeded(response);
                    UpdateCollection(Issues, response.Content?.Items, replace);
                    UpdateSession(session, response.Content, response.Headers);
                    break;
                }
            case SearchType.PullRequests:
                {
                    var response = await api.SearchPullRequests(EncodeQuery($"{query} is:pr"))
                        .FirstAsync(cancellationToken);
                    if (!IsCurrent(version))
                        return;
                    EnsureSearchSucceeded(response);
                    UpdateCollection(PullRequests, response.Content?.Items, replace);
                    UpdateSession(session, response.Content, response.Headers);
                    break;
                }
            case SearchType.Code:
                {
                    var response = await api.SearchCode(EncodeQuery(query)).FirstAsync(cancellationToken);
                    if (!IsCurrent(version))
                        return;
                    EnsureSearchSucceeded(response);
                    UpdateCollection(CodeResults, response.Content?.Items, replace);
                    UpdateSession(session, response.Content, response.Headers);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private async Task LoadInboxAsync(SearchInbox inbox)
    {
        var (version, requestCts) = BeginRequest();
        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var result = await inbox.LoadAsync(
                _clientFactory, version, IsCurrent, requestCts.Token);
            if (!result.Completed)
                return;

            if (result.Error is not null)
                ErrorMessage.Value = result.Error;

            RefreshSelectedState();
        }
        finally
        {
            CompleteRequest(version, requestCts);
        }
    }

    private async Task LoadMoreInboxAsync(SearchInbox inbox)
    {
        if (!inbox.CanLoadMore || IsLoading.Value)
            return;

        var (version, requestCts) = BeginRequest();
        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var result = await inbox.LoadMoreAsync(
                version, IsCurrent, requestCts.Token);
            if (!result.Completed)
                return;

            if (result.Error is not null)
                ErrorMessage.Value = result.Error;

            RefreshSelectedState();
        }
        finally
        {
            CompleteRequest(version, requestCts);
        }
    }

    private void ApplyInboxState(SearchInbox inbox)
    {
        HasSearched.Value = inbox.HasSearched;
        IsEmpty.Value = inbox.HasSearched && inbox.Items.Count == 0;
        TotalCount.Value = inbox.TotalCount;
        CanLoadMore.Value = inbox.HasNextPage;
    }

    private bool IsAnyInboxHub =>
        IsReviewRequestedHub.Value || IsAssignedHub.Value || IsMentionsHub.Value;

    private bool HubSessionReady(string hub)
    {
        if (string.Equals(hub, SearchHub, StringComparison.Ordinal))
            return true;
        if (string.Equals(hub, ReviewRequestedHub, StringComparison.Ordinal))
            return _reviewInbox.HasSession;
        if (string.Equals(hub, AssignedHub, StringComparison.Ordinal))
            return _assignedInbox.HasSession;
        if (string.Equals(hub, MentionsHub, StringComparison.Ordinal))
            return _mentionsInbox.HasSession;
        return false;
    }

    private void ApplyHub(string hub)
    {
        CancelActiveRequest();
        IsLoading.Value = false;
        ErrorMessage.Value = string.Empty;

        SelectedHub.Value = hub;
        IsSearchHub.Value = string.Equals(hub, SearchHub, StringComparison.Ordinal);
        IsReviewRequestedHub.Value = string.Equals(hub, ReviewRequestedHub, StringComparison.Ordinal);
        IsAssignedHub.Value = string.Equals(hub, AssignedHub, StringComparison.Ordinal);
        IsMentionsHub.Value = string.Equals(hub, MentionsHub, StringComparison.Ordinal);
        ApplyCollectionVisibility();
    }

    private void OnSelectedTypeChanged(SearchType type)
    {
        if (IsAnyInboxHub)
            return;

        CancelActiveRequest();
        IsLoading.Value = false;
        ErrorMessage.Value = string.Empty;
        ApplyCollectionVisibility();
        RefreshSelectedState();
    }

    private void ApplyCollectionVisibility()
    {
        var type = SelectedType.Value;
        var review = IsReviewRequestedHub.Value;
        var assigned = IsAssignedHub.Value;
        var mentions = IsMentionsHub.Value;
        var inbox = review || assigned || mentions;

        IsReviewRequestedSelected.Value = review;
        IsAssignedSelected.Value = assigned;
        IsMentionsSelected.Value = mentions;
        IsRepositoriesSelected.Value = !inbox && type == SearchType.Repositories;
        IsIssuesSelected.Value = !inbox && type == SearchType.Issues;
        IsPullRequestsSelected.Value = !inbox && type == SearchType.PullRequests;
        IsCodeSelected.Value = !inbox && type == SearchType.Code;
    }

    private void RefreshSelectedState()
    {
        if (IsReviewRequestedHub.Value)
        {
            ApplyInboxState(_reviewInbox);
            return;
        }

        if (IsAssignedHub.Value)
        {
            ApplyInboxState(_assignedInbox);
            return;
        }

        if (IsMentionsHub.Value)
        {
            ApplyInboxState(_mentionsInbox);
            return;
        }

        var type = SelectedType.Value;
        var searchSession = _sessions[type];
        var count = GetResultCount(type);

        HasSearched.Value = searchSession.HasSearched;
        IsEmpty.Value = searchSession.HasSearched && count == 0;
        TotalCount.Value = searchSession.TotalCount;
        CanLoadMore.Value = searchSession.HasNextPage;
    }

    private int GetResultCount(SearchType type)
    {
        return type switch
        {
            SearchType.Repositories => Repositories.Count,
            SearchType.Issues => Issues.Count,
            SearchType.PullRequests => PullRequests.Count,
            SearchType.Code => CodeResults.Count,
            _ => 0,
        };
    }

    private static void UpdateCollection<T>(
        ObservableCollection<T> collection,
        IEnumerable<T>? items,
        bool replace)
    {
        if (replace)
            collection.Clear();

        foreach (var item in items ?? [])
            collection.Add(item);
    }

    private static void UpdateSession<T>(
        SearchSession session,
        SearchResult<T>? result,
        System.Net.Http.Headers.HttpResponseHeaders? headers)
    {
        session.TotalCount = result?.TotalCount ?? 0;
        session.Paged?.ApplyLink(headers);
    }

    private static string EncodeQuery(string query)
    {
        return Uri.EscapeDataString(query);
    }

    private static void EnsureSearchSucceeded<T>(ApiResponse<T> response)
    {
        if (!response.IsSuccessStatusCode)
            throw new SearchRequestException(
                response.StatusCode ?? HttpStatusCode.ServiceUnavailable);
    }


    private async Task<PagedGitHubSession?> StartPagedAsync(
        SearchSession session,
        string query,
        int version,
        CancellationToken cancellationToken)
    {
        var paged = await _clientFactory.CreatePagedSessionAsync(cancellationToken);
        if (!IsCurrent(version))
        {
            paged.Dispose();
            return null;
        }

        if (paged.Client.DefaultRequestHeaders.Authorization is null)
        {
            paged.Dispose();
            ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
            return null;
        }

        session.Paged = paged;
        session.Query = query;
        paged.Reset();
        paged.PrepareRequest();
        return paged;
    }

    private (int Version, CancellationTokenSource RequestCts) BeginRequest()
    {
        _requestCts?.Cancel();
        var requestCts = new CancellationTokenSource(RequestTimeout);
        _requestCts = requestCts;
        return (Interlocked.Increment(ref _requestVersion), requestCts);
    }

    private void CompleteRequest(int version, CancellationTokenSource requestCts)
    {
        if (IsCurrent(version))
        {
            IsLoading.Value = false;
            if (ReferenceEquals(_requestCts, requestCts))
                _requestCts = null;
        }

        requestCts.Dispose();
    }

    private void CancelActiveRequest()
    {
        Interlocked.Increment(ref _requestVersion);
        _requestCts?.Cancel();
        _requestCts = null;
    }

    private bool IsCurrent(int version)
    {
        return Volatile.Read(ref _requestVersion) == version;
    }

    public void Dispose()
    {
        CancelActiveRequest();
        _disposables.Dispose();
        foreach (var session in _sessions.Values)
            session.DisposePaged();
        _reviewInbox.Dispose();
        _assignedInbox.Dispose();
        _mentionsInbox.Dispose();

        Query.Dispose();
        SelectedType.Dispose();
        SelectedHub.Dispose();
        IsLoading.Dispose();
        CanLoadMore.Dispose();
        HasSearched.Dispose();
        IsEmpty.Dispose();
        TotalCount.Dispose();
        ErrorMessage.Dispose();
        IsSearchHub.Dispose();
        IsReviewRequestedHub.Dispose();
        IsAssignedHub.Dispose();
        IsMentionsHub.Dispose();
        IsRepositoriesSelected.Dispose();
        IsIssuesSelected.Dispose();
        IsPullRequestsSelected.Dispose();
        IsCodeSelected.Dispose();
        IsReviewRequestedSelected.Dispose();
        IsAssignedSelected.Dispose();
        IsMentionsSelected.Dispose();
    }

    private sealed class SearchSession
    {
        public PagedGitHubSession? Paged { get; set; }
        public string Query { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public bool HasSearched { get; set; }
        public bool HasNextPage => Paged?.HasNextPage == true;

        public void DisposePaged()
        {
            Paged?.Dispose();
            Paged = null;
        }
    }

    private sealed class SearchRequestException(HttpStatusCode statusCode) : Exception
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}
