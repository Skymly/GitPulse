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
/// Supports pagination via <see cref="ApiResponse{T}"/> + <c>Link</c> header.
/// </summary>
public sealed partial class ReposViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];

    private HttpClient? _pagedClient;
    private GitHubQueryHandler? _queryHandler;
    private int _currentPage;
    private bool _hasNextPage;

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

    public ReposViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;

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
            _pagedClient?.Dispose();
            _allRepos.Clear();

            var (client, queryHandler) = await _clientFactory.CreatePagedClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                IsAuthenticated.Value = false;
                client.Dispose();
                return;
            }

            IsAuthenticated.Value = true;
            _pagedClient = client;
            _queryHandler = queryHandler;
            _queryHandler.Page = 1;
            _queryHandler.PerPage = 30;

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListMyReposPaged().FirstAsync(cts.Token);

            _allRepos.AddRange(response.Content ?? []);
            ApplyFilter(SearchText.Value);

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

    /// <summary>Load the next page of repositories (appends to the list).</summary>
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
            var response = await api.ListMyReposPaged().FirstAsync(cts.Token);

            _allRepos.AddRange(response.Content ?? []);
            ApplyFilter(SearchText.Value);

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
        SearchText.Dispose();
        IsLoading.Dispose();
        IsAuthenticated.Dispose();
        CanLoadMore.Dispose();
        ErrorMessage.Dispose();
        _pagedClient?.Dispose();
    }
}
