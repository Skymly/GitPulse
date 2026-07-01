using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Issue detail view model — shows a single issue and its comments.
/// Demonstrates <see cref="IGitHubReposApi.GetIssue"/>,
/// <see cref="IGitHubReposApi.ListIssueComments"/>, and M3 CRUD operations:
/// <see cref="IGitHubReposApi.CreateIssueComment"/>,
/// <see cref="IGitHubReposApi.UpdateIssue"/> (state toggle),
/// <see cref="IGitHubReposApi.ReplaceIssueLabels"/>.
/// </summary>
public sealed partial class IssueDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private int _issueNumber;

    /// <summary>The issue being viewed.</summary>
    public BindableReactiveProperty<Issue?> Issue { get; } = new(null);

    /// <summary>Comments on the issue.</summary>
    public ObservableCollection<Comment> Comments { get; } = [];

    /// <summary>Labels on the issue (editable via <see cref="SaveLabelsCommand"/>).</summary>
    public ObservableCollection<Label> Labels { get; } = [];

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Whether a write operation (comment/state/labels) is in progress.</summary>
    public BindableReactiveProperty<bool> IsSaving { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Issue title for the page header.</summary>
    public BindableReactiveProperty<string> Title { get; } = new(string.Empty);

    /// <summary>Comment input text (two-way bound to editor).</summary>
    public BindableReactiveProperty<string> CommentInput { get; } = new(string.Empty);

    /// <summary>Comma-separated label names for editing (two-way bound to entry).</summary>
    public BindableReactiveProperty<string> LabelInput { get; } = new(string.Empty);

    public IssueDetailViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
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

            var issue = await api.GetIssue(_owner, _repo, _issueNumber).FirstAsync(cts.Token);
            Issue.Value = issue;
            Title.Value = $"#{issue.Number} {issue.Title}";

            // Populate labels from the issue payload.
            Labels.Clear();
            foreach (var label in issue.Labels)
                Labels.Add(label);
            LabelInput.Value = string.Join(", ", issue.Labels.Select(l => l.Name));

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

    /// <summary>Post a new comment on the issue.</summary>
    [RelayCommand]
    private async Task AddCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(CommentInput.Value) || IsSaving.Value)
            return;

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var request = new CommentCreateRequest { Body = CommentInput.Value };
            var comment = await api.CreateIssueComment(_owner, _repo, _issueNumber, request)
                .FirstAsync(cts.Token);

            Comments.Add(comment);
            CommentInput.Value = string.Empty;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Comment failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    /// <summary>Toggle the issue state between "open" and "closed".</summary>
    [RelayCommand]
    private async Task ToggleStateAsync()
    {
        if (Issue.Value is null || IsSaving.Value)
            return;

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var newState = Issue.Value.State == "open" ? "closed" : "open";
            var request = new IssueUpdateRequest { State = newState };
            var updated = await api.UpdateIssue(_owner, _repo, _issueNumber, request)
                .FirstAsync(cts.Token);

            Issue.Value = updated;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"State change failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    /// <summary>Replace the issue's labels with the comma-separated names in <see cref="LabelInput"/>.</summary>
    [RelayCommand]
    private async Task SaveLabelsAsync()
    {
        if (IsSaving.Value)
            return;

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var names = LabelInput.Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToArray();

            var request = new LabelsReplaceRequest { Labels = names };
            var updatedLabels = await api.ReplaceIssueLabels(_owner, _repo, _issueNumber, request)
                .FirstAsync(cts.Token);

            Labels.Clear();
            foreach (var label in updatedLabels)
                Labels.Add(label);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Labels save failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    public void Dispose()
    {
        Issue.Dispose();
        IsLoading.Dispose();
        IsSaving.Dispose();
        ErrorMessage.Dispose();
        Title.Dispose();
        CommentInput.Dispose();
        LabelInput.Dispose();
    }
}
