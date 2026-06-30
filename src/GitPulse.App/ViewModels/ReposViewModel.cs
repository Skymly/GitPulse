using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.App.ViewModels;

/// <summary>
/// Repository list view model — the primary showcase of
/// <see cref="IGitHubReposApi"/> (Observables.RestAPI.R3) and
/// R3 <see cref="BindableReactiveProperty{T}"/> state management.
/// </summary>
public sealed partial class ReposViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>Repos currently displayed.</summary>
    public ObservableCollection<Repo> Repos { get; } = [];

    /// <summary>Filtered view of <see cref="Repos"/> based on <see cref="SearchText"/>.</summary>
    public ReadOnlyObservableCollection<Repo> FilteredRepos { get; }

    /// <summary>Search box text (two-way bound; debounced via Events domain in code-behind).</summary>
    public BindableReactiveProperty<string> SearchText { get; } = new(string.Empty);

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Whether the user is authenticated (has a stored token).</summary>
    public BindableReactiveProperty<bool> IsAuthenticated { get; } = new(false);

    /// <summary>Error message shown on failure; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    public ReposViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;

        // Reactive filter: whenever Repos changes or SearchText changes,
        // recompute the filtered list.
        FilteredRepos = new ReadOnlyObservableCollection<Repo>(Repos);

        // Subscribe to SearchText changes to filter the list in real-time.
        // The debounce (Throttle) is applied in the page code-behind via
        // the Observables.Events domain — this handler does the actual filter.
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
        // The client factory reads the token from ICredentialStore internally;
        // if the token is absent, IsAuthenticated stays false.
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
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                IsAuthenticated.Value = false;
                return;
            }

            IsAuthenticated.Value = true;
            var api = RestService.For<IGitHubReposApi>(client);

            // Observables.RestAPI returns an Observable<Repo[]>; we await the
            // first emission. This is the core RestAPI domain showcase.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var repos = await api.ListMyRepos().FirstAsync(cts.Token);

            _allRepos.Clear();
            _allRepos.AddRange(repos);
            ApplyFilter(SearchText.Value);
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

    public void Dispose()
    {
        _disposables.Dispose();
        SearchText.Dispose();
        IsLoading.Dispose();
        IsAuthenticated.Dispose();
        ErrorMessage.Dispose();
    }
}
