using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.App.ViewModels;

/// <summary>
/// Issue detail view model — shows a single issue and its comments.
/// Demonstrates <see cref="IGitHubReposApi.GetIssue"/> and
/// <see cref="IGitHubReposApi.ListIssueComments"/>.
/// </summary>
public sealed partial class IssueDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private int _issueNumber;

    /// <summary>The issue being viewed.</summary>
    public BindableReactiveProperty<Issue?> Issue { get; } = new(null);

    /// <summary>Comments on the issue.</summary>
    public ObservableCollection<Comment> Comments { get; } = [];

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Issue title for the page header.</summary>
    public BindableReactiveProperty<string> Title { get; } = new(string.Empty);

    public IssueDetailViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    [RelayCommand]
    private async Task OpenInBrowserAsync(string url)
    {
        if (!string.IsNullOrEmpty(url))
            await Launcher.OpenAsync(url);
    }

    public void Initialize(string owner, string repo, int issueNumber)
    {
        _owner = owner;
        _repo = repo;
        _issueNumber = issueNumber;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || _issueNumber <= 0)
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

            // Load issue detail and comments sequentially (R3 FirstAsync
            // returns an awaitable, not Task<T>, so parallel Task.WhenAll
            // is not directly applicable).
            var issue = await api.GetIssue(_owner, _repo, _issueNumber).FirstAsync(cts.Token);
            Issue.Value = issue;
            Title.Value = $"#{issue.Number} {issue.Title}";

            var comments = await api.ListIssueComments(_owner, _repo, _issueNumber).FirstAsync(cts.Token);
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
        Issue.Dispose();
        IsLoading.Dispose();
        ErrorMessage.Dispose();
        Title.Dispose();
    }
}
