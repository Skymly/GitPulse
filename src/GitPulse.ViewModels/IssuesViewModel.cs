using System.Collections.ObjectModel;
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
/// via <see cref="PagedGitHubSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// The ViewModel creates a fresh <see cref="PagedGitHubSession"/> on each load
/// (so credential changes are picked up) and holds it so that page cursor and
/// <c>State</c> persist between the initial load and subsequent "load more"
/// requests. The session writes these values onto
/// <see cref="GitHubQueryHandler"/> at the HTTP layer, working around the
/// Observables OBS3004 limitation that prevents <c>[Query]</c> parameters on
/// declarative interface methods with path parameters.
/// </para>
/// </remarks>
public sealed partial class IssuesViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private readonly PagedListCycle _cycle;

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
        _cycle = new PagedListCycle(clientFactory);
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
        // Filter change reloads from page 1 once a session cycle has started.
        // Load recreates the session so credential changes apply.
        if (_cycle.HasSession)
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
            var result = await _cycle.LoadAsync(StateFilter.Value, async (client, ct) =>
            {
                var api = RestService.For<IGitHubReposApi>(client);
                var response = await api.ListIssuesPaged(_owner, _repo).FirstAsync(ct);
                return new PagedListPage<Issue>(response.Content ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                return;
            }

            Issues.Clear();
            foreach (var issue in result.Items)
                Issues.Add(issue);
            CanLoadMore.Value = result.HasNextPage;
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
        if (!_cycle.CanLoadMore || IsLoading.Value)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var result = await _cycle.LoadMoreAsync(async (client, ct) =>
            {
                var api = RestService.For<IGitHubReposApi>(client);
                var response = await api.ListIssuesPaged(_owner, _repo).FirstAsync(ct);
                return new PagedListPage<Issue>(response.Content ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                return;
            }

            foreach (var issue in result.Items)
                Issues.Add(issue);
            CanLoadMore.Value = result.HasNextPage;
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
        _cycle.Dispose();
    }
}
