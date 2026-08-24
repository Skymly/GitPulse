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
/// Repository list view model — the primary showcase of
/// <see cref="IGitHubReposApi"/> (Observables.RestAPI.R3) and
/// R3 <see cref="BindableReactiveProperty{T}"/> state management.
/// Supports pagination via <see cref="PagedGitHubSession"/>.
/// M17 adds a My repos / Starred hub switch via
/// <see cref="IGitHubReposApi.ListStarredReposPaged"/>.
/// M25 lists My repos with <c>sort=pushed</c>.
/// </summary>
public sealed partial class ReposViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];

    private readonly PagedListCycle _cycle;

    /// <summary>Repos currently displayed (after search filter).</summary>
    public ObservableCollection<Repo> Repos { get; } = [];

    /// <summary>Filtered view of <see cref="Repos"/> based on <see cref="SearchText"/>.</summary>
    public ReadOnlyObservableCollection<Repo> FilteredRepos { get; }

    /// <summary>Search box text (two-way bound; debounced via Events domain in code-behind).</summary>
    public BindableReactiveProperty<string> SearchText { get; } = new(string.Empty);

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Whether the user is authenticated (has a stored token).</summary>
    public BindableReactiveProperty<bool> IsAuthenticated { get; } = new(false);

    /// <summary>Whether more pages can be loaded.</summary>
    public BindableReactiveProperty<bool> CanLoadMore { get; } = new(false);

    /// <summary>Error message shown on failure; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    public const string MyReposHub = "My repos";
    public const string StarredHub = "Starred";

    public const string MyReposSort = "pushed";

    /// <summary>Hub options shown on the Repos tab.</summary>
    public ObservableCollection<string> HubOptions { get; } = [MyReposHub, StarredHub];

    /// <summary>Active hub: My repos (default) or Starred.</summary>
    public BindableReactiveProperty<string> SelectedHub { get; } = new(MyReposHub);

    public ReposViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
        _cycle = new PagedListCycle(clientFactory);

        FilteredRepos = new ReadOnlyObservableCollection<Repo>(Repos);

        // Subscribe to SearchText changes to filter the list in real-time.
        // The debounce is applied in the page code-behind via the
        // Observables.Events domain — this handler does the actual filter.
        SearchText.Subscribe(ApplyFilter).AddTo(_disposables);

        _ = CheckAuthAsync();
    }

    private readonly List<Repo> _allRepos = [];

    private void ApplyFilter(string search)
    {
        Repos.Clear();
        if (string.IsNullOrWhiteSpace(search))
        {
            foreach (var r in _allRepos) Repos.Add(r);
        }
        else
        {
            var lower = search.ToLowerInvariant();
            foreach (var r in _allRepos)
            {
                if (r.Name.Contains(lower, StringComparison.OrdinalIgnoreCase)
                    || (r.Description?.Contains(lower, StringComparison.OrdinalIgnoreCase) == true))
                {
                    Repos.Add(r);
                }
            }
        }
    }

    private async Task CheckAuthAsync()
    {
        var client = await _clientFactory.CreateClientAsync();
        IsAuthenticated.Value = client.DefaultRequestHeaders.Authorization is not null;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            _allRepos.Clear();
            ApplyFilter(SearchText.Value);

            var result = await _cycle.LoadAsync(null, async (client, ct) =>
            {
                var api = RestService.For<IGitHubReposApi>(client);
                var response = await ListCurrentAsync(api, ct);
                return new PagedListPage<Repo>(response.Content ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                IsAuthenticated.Value = result.Authenticated;
                return;
            }

            IsAuthenticated.Value = true;
            _allRepos.AddRange(result.Items);
            ApplyFilter(SearchText.Value);
            CanLoadMore.Value = result.HasNextPage;
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    /// <summary>Load the next page of repositories (appends to the list).</summary>
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
                var response = await ListCurrentAsync(api, ct);
                return new PagedListPage<Repo>(response.Content ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                return;
            }

            _allRepos.AddRange(result.Items);
            ApplyFilter(SearchText.Value);
            CanLoadMore.Value = result.HasNextPage;
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    [RelayCommand]
    private async Task SelectHubAsync(string? hub)
    {
        var next = string.IsNullOrWhiteSpace(hub) ? MyReposHub : hub;
        if (string.Equals(SelectedHub.Value, next, StringComparison.Ordinal) && _cycle.HasSession)
            return;

        SelectedHub.Value = next;
        await LoadAsync();
    }

    private Task<ApiResponse<Repo[]>> ListCurrentAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        var request = string.Equals(SelectedHub.Value, StarredHub, StringComparison.Ordinal)
            ? api.ListStarredReposPaged()
            : api.ListMyReposSortedPaged(MyReposSort);
        return request.FirstAsync(cancellationToken);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        SearchText.Dispose();
        IsLoading.Dispose();
        IsAuthenticated.Dispose();
        CanLoadMore.Dispose();
        ErrorMessage.Dispose();
        SelectedHub.Dispose();
        _cycle.Dispose();
    }
}
