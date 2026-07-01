using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Pull requests list view model for a specific repository. Demonstrates
/// <see cref="IGitHubReposApi.ListPullRequests"/> and reactive state filtering
/// (open / closed / all) via R3 <see cref="BindableReactiveProperty{T}"/>.
/// Mirrors <see cref="IssuesViewModel"/> but operates on
/// <see cref="PullRequest"/> models.
/// </summary>
public sealed partial class PullRequestsViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly CompositeDisposable _disposables = [];

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private readonly List<PullRequest> _allPrs = [];

    /// <summary>Pull requests currently displayed (after state filter).</summary>
    public ObservableCollection<PullRequest> PullRequests { get; } = [];

    /// <summary>Filter: "open", "closed", or "all".</summary>
    public BindableReactiveProperty<string> StateFilter { get; } = new("open");

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Repository full name for display (owner/repo).</summary>
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    /// <summary>Owner part (for navigation to PR detail).</summary>
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);

    /// <summary>Repo name part (for navigation to PR detail).</summary>
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);

    public PullRequestsViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
        StateFilter.Subscribe(ApplyStateFilter).AddTo(_disposables);
    }

    /// <summary>
    /// Initialize with repository coordinates. Called by the page when
    /// navigated to via Shell query parameters.
    /// </summary>
    public void Initialize(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        Owner.Value = owner;
        RepoName.Value = repo;
        RepoFullName.Value = $"{owner}/{repo}";
    }

    private void ApplyStateFilter(string state)
    {
        PullRequests.Clear();
        foreach (var pr in _allPrs)
        {
            if (state == "all" || pr.State == state)
                PullRequests.Add(pr);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var prs = await api.ListPullRequests(_owner, _repo).FirstAsync(cts.Token);

            _allPrs.Clear();
            _allPrs.AddRange(prs);
            ApplyStateFilter(StateFilter.Value);
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
        StateFilter.Dispose();
        IsLoading.Dispose();
        ErrorMessage.Dispose();
        RepoFullName.Dispose();
        Owner.Dispose();
        RepoName.Dispose();
    }
}
