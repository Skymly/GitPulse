using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Pull request detail view model — shows a single PR and its conversation
/// comments. Demonstrates <see cref="IGitHubReposApi.GetPullRequest"/> and
/// <see cref="IGitHubReposApi.ListIssueComments"/> (PR conversation comments
/// share the issue comments endpoint in the GitHub REST API).
/// </summary>
public sealed partial class PullRequestDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private int _prNumber;

    /// <summary>The pull request being viewed.</summary>
    public BindableReactiveProperty<PullRequest?> PullRequest { get; } = new(null);

    /// <summary>Conversation comments on the PR.</summary>
    public ObservableCollection<Comment> Comments { get; } = [];

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>PR title for the page header.</summary>
    public BindableReactiveProperty<string> Title { get; } = new(string.Empty);

    public PullRequestDetailViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    [RelayCommand]
    private async Task OpenInBrowserAsync(string url)
    {
        if (!string.IsNullOrEmpty(url))
            await _browserLauncher.OpenAsync(url);
    }

    public void Initialize(string owner, string repo, int prNumber)
    {
        _owner = owner;
        _repo = repo;
        _prNumber = prNumber;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || _prNumber <= 0)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Load PR detail, then conversation comments. PR conversation
            // comments use the issue comments endpoint (GitHub REST API).
            var pr = await api.GetPullRequest(_owner, _repo, _prNumber).FirstAsync(cts.Token);
            PullRequest.Value = pr;
            Title.Value = $"#{pr.Number} {pr.Title}";

            var comments = await api.ListIssueComments(_owner, _repo, _prNumber).FirstAsync(cts.Token);
            Comments.Clear();
            foreach (var comment in comments)
                Comments.Add(comment);
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
        PullRequest.Dispose();
        IsLoading.Dispose();
        ErrorMessage.Dispose();
        Title.Dispose();
    }
}
