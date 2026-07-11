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

public sealed partial class SearchViewModel : IDisposable
{
    private const int MinimumQueryLength = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];
    private readonly Dictionary<SearchType, SearchSession> _sessions = [];

    private CancellationTokenSource? _requestCts;
    private int _requestVersion;

    public ObservableCollection<Repo> Repositories { get; } = [];
    public ObservableCollection<SearchIssueItem> Issues { get; } = [];
    public ObservableCollection<SearchIssueItem> PullRequests { get; } = [];
    public ObservableCollection<CodeSearchItem> CodeResults { get; } = [];

    public BindableReactiveProperty<string> Query { get; } = new(string.Empty);
    public BindableReactiveProperty<SearchType> SelectedType { get; } = new(SearchType.Repositories);
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<bool> CanLoadMore { get; } = new(false);
    public BindableReactiveProperty<bool> HasSearched { get; } = new(false);
    public BindableReactiveProperty<bool> IsEmpty { get; } = new(false);
    public BindableReactiveProperty<int> TotalCount { get; } = new(0);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    public BindableReactiveProperty<bool> IsRepositoriesSelected { get; } = new(true);
    public BindableReactiveProperty<bool> IsIssuesSelected { get; } = new(false);
    public BindableReactiveProperty<bool> IsPullRequestsSelected { get; } = new(false);
    public BindableReactiveProperty<bool> IsCodeSelected { get; } = new(false);

    public SearchViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;

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
        SelectedType.Value = type;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SearchAsync()
    {
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
        session.DisposeClient();

        var (version, requestCts) = BeginRequest();
        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var (client, queryHandler) =
                await _clientFactory.CreatePagedClientAsync(requestCts.Token);

            if (!IsCurrent(version))
            {
                client.Dispose();
                return;
            }

            if (client.DefaultRequestHeaders.Authorization is null)
            {
                client.Dispose();
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                return;
            }

            session.Client = client;
            session.QueryHandler = queryHandler;
            session.Query = query;
            session.CurrentPage = 1;
            queryHandler.Page = 1;
            queryHandler.PerPage = 30;

            var api = RestService.For<IGitHubSearchApi>(client);
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
        var type = SelectedType.Value;
        var session = _sessions[type];
        if (!session.HasNextPage
            || session.Client is null
            || session.QueryHandler is null
            || IsLoading.Value)
        {
            return;
        }

        var (version, requestCts) = BeginRequest();
        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            session.QueryHandler.Page = session.CurrentPage + 1;
            var api = RestService.For<IGitHubSearchApi>(session.Client);
            await SearchPageAsync(
                api, type, session.Query, session, replace: false, version, requestCts.Token);

            if (!IsCurrent(version))
                return;

            session.CurrentPage++;
            RefreshSelectedState();
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(version))
                ErrorMessage.Value = "Request timed out.";
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
                var response = await api.SearchRepositories(query).FirstAsync(cancellationToken);
                if (!IsCurrent(version))
                    return;
                UpdateCollection(Repositories, response.Content?.Items, replace);
                UpdateSession(session, response.Content, response.Headers);
                break;
            }
            case SearchType.Issues:
            {
                var response = await api.SearchIssues($"{query} is:issue").FirstAsync(cancellationToken);
                if (!IsCurrent(version))
                    return;
                UpdateCollection(Issues, response.Content?.Items, replace);
                UpdateSession(session, response.Content, response.Headers);
                break;
            }
            case SearchType.PullRequests:
            {
                var response = await api.SearchPullRequests($"{query} is:pr").FirstAsync(cancellationToken);
                if (!IsCurrent(version))
                    return;
                UpdateCollection(PullRequests, response.Content?.Items, replace);
                UpdateSession(session, response.Content, response.Headers);
                break;
            }
            case SearchType.Code:
            {
                var response = await api.SearchCode(query).FirstAsync(cancellationToken);
                if (!IsCurrent(version))
                    return;
                UpdateCollection(CodeResults, response.Content?.Items, replace);
                UpdateSession(session, response.Content, response.Headers);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private void OnSelectedTypeChanged(SearchType type)
    {
        CancelActiveRequest();
        IsLoading.Value = false;
        ErrorMessage.Value = string.Empty;

        IsRepositoriesSelected.Value = type == SearchType.Repositories;
        IsIssuesSelected.Value = type == SearchType.Issues;
        IsPullRequestsSelected.Value = type == SearchType.PullRequests;
        IsCodeSelected.Value = type == SearchType.Code;

        RefreshSelectedState();
    }

    private void RefreshSelectedState()
    {
        var type = SelectedType.Value;
        var session = _sessions[type];
        var count = GetResultCount(type);

        HasSearched.Value = session.HasSearched;
        IsEmpty.Value = session.HasSearched && count == 0;
        TotalCount.Value = session.TotalCount;
        CanLoadMore.Value = session.HasNextPage;
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
        session.HasNextPage = LinkHeaderParser.GetNextUrl(headers) is not null;
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
            session.DisposeClient();

        Query.Dispose();
        SelectedType.Dispose();
        IsLoading.Dispose();
        CanLoadMore.Dispose();
        HasSearched.Dispose();
        IsEmpty.Dispose();
        TotalCount.Dispose();
        ErrorMessage.Dispose();
        IsRepositoriesSelected.Dispose();
        IsIssuesSelected.Dispose();
        IsPullRequestsSelected.Dispose();
        IsCodeSelected.Dispose();
    }

    private sealed class SearchSession
    {
        public HttpClient? Client { get; set; }
        public GitHubQueryHandler? QueryHandler { get; set; }
        public string Query { get; set; } = string.Empty;
        public int CurrentPage { get; set; }
        public int TotalCount { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasSearched { get; set; }

        public void DisposeClient()
        {
            Client?.Dispose();
            Client = null;
            QueryHandler = null;
            HasNextPage = false;
        }
    }
}
