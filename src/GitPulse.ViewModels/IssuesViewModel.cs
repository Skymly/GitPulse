using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Issues list view model for a specific repository. Demonstrates
/// <see cref="IGitHubReposApi.ListIssuesPaged"/> returning
/// <see cref="ApiResponse{T}"/> (exposing the <c>Link</c> header for
/// pagination), reactive state filtering (open / closed / all) via R3
/// <see cref="BindableReactiveProperty{T}"/>, and server-side pagination
/// via <see cref="GitHubQueryHandler"/>.
/// </summary>
/// <remarks>
/// <para>
/// The ViewModel holds a <see cref="GitHubQueryHandler"/> across load calls
/// so that <c>Page</c>/<c>State</c> state persists between the initial load
/// and subsequent "load more" requests. The handler injects these values as
/// query parameters at the HTTP layer, working around the Observables 0.1.4
/// OBS3004 limitation that prevents <c>[Query]</c> parameters on declarative
/// interface methods with path parameters.
/// </para>
/// </remarks>
public sealed partial class IssuesViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private HttpClient? _pagedClient;
    private GitHubQueryHandler? _queryHandler;
    private int _currentPage;
    private bool _hasNextPage;

    /// <summary>Issues currently displayed.</summary>
    public ObservableCollection<Issue> Issues { get; } = [];

    /// <summary>Filter: "open", "closed", or "all".</summary>
    public BindableReactiveProperty<string> StateFilter { get; } = new("open");

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Whether more pages can be loaded.</summary>
    public BindableReactiveProperty<bool> CanLoadMore { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Repository full name for display (owner/repo).</summary>
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    /// <summary>Owner part (for navigation to issue detail).</summary>
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);

    /// <summary>Repo name part (for navigation to issue detail).</summary>
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);

    public IssuesViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
        StateFilter.Subscribe(OnStateChanged).AddTo(_disposables);
    }

    /// <summary>
    /// Initialize with repository coordinates. Called by the page when
    /// navigated to with <see cref="RepoNavigationArgs"/>.
    /// </summary>
    public void Initialize(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        Owner.Value = owner;
        RepoName.Value = repo;
        RepoFullName.Value = $"{owner}/{repo}";
    }

    private void OnStateChanged(string state)
    {
        // State filter is server-side via query handler.
        // When the filter changes, reload from page 1 (if already initialized).
        if (_queryHandler is not null)
            _ = LoadCommand.ExecuteAsync(null);
    }

    /// <summary>Initial load (page 1) or reload after filter change.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            // Dispose previous paged client if reloading.
            _pagedClient?.Dispose();

            var (client, queryHandler) = await _clientFactory.CreatePagedClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                client.Dispose();
                return;
            }

            _pagedClient = client;
            _queryHandler = queryHandler;
            _queryHandler.State = StateFilter.Value;
            _queryHandler.Page = 1;
            _queryHandler.PerPage = 30;

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListIssuesPaged(_owner, _repo).FirstAsync(cts.Token);

            Issues.Clear();
            foreach (var issue in response.Content ?? [])
                Issues.Add(issue);

            _currentPage = 1;
            _hasNextPage = LinkHeaderParser.GetNextUrl(response.Headers) is not null;
            CanLoadMore.Value = _hasNextPage;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    /// <summary>Load the next page of issues (appends to the list).</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_hasNextPage || _queryHandler is null || _pagedClient is null || IsLoading.Value)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            _queryHandler.Page = _currentPage + 1;

            var api = RestService.For<IGitHubReposApi>(_pagedClient);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListIssuesPaged(_owner, _repo).FirstAsync(cts.Token);

            foreach (var issue in response.Content ?? [])
                Issues.Add(issue);

            _currentPage++;
            _hasNextPage = LinkHeaderParser.GetNextUrl(response.Headers) is not null;
            CanLoadMore.Value = _hasNextPage;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Load more failed: {ex.Message}";
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        StateFilter.Dispose();
        IsLoading.Dispose();
        CanLoadMore.Dispose();
        ErrorMessage.Dispose();
        RepoFullName.Dispose();
        Owner.Dispose();
        RepoName.Dispose();
        _pagedClient?.Dispose();
    }
}
