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
/// <see cref="IGitHubReposApi.ReplaceIssueLabels"/>,
/// <see cref="IGitHubReposApi.AddIssueAssignees"/>.
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

    /// <summary>Users assigned to the issue.</summary>
    public ObservableCollection<User> Assignees { get; } = [];

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

    /// <summary>GitHub login to assign.</summary>
    public BindableReactiveProperty<string> AssigneeLogin { get; } = new(string.Empty);

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
            ApplyAssignees(issue.Assignees);

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

    /// <summary>Assign the typed GitHub login.</summary>
    [RelayCommand]
    private async Task AddAssigneeAsync()
    {
        var login = AssigneeLogin.Value.Trim().TrimStart('@');
        if (login.Length == 0 || Issue.Value is null || IsSaving.Value)
            return;

        if (Assignees.Any(user =>
                string.Equals(user.Login, login, StringComparison.OrdinalIgnoreCase)))
            return;

        await WriteAssigneesAsync(
            requesting: true,
            login,
            api => api.AddIssueAssignees(
                _owner, _repo, _issueNumber, new AssigneesRequest { Assignees = [login] }));
    }

    /// <summary>Remove an assignee by login.</summary>
    [RelayCommand]
    private async Task RemoveAssigneeAsync(string? login)
    {
        login = login?.Trim();
        if (string.IsNullOrEmpty(login) || Issue.Value is null || IsSaving.Value)
            return;

        await WriteAssigneesAsync(
            requesting: false,
            login,
            api => api.RemoveIssueAssignees(
                _owner, _repo, _issueNumber, new AssigneesRequest { Assignees = [login] }));
    }

    private async Task WriteAssigneesAsync(
        bool requesting,
        string login,
        Func<IGitHubReposApi, R3.Observable<Observables.RestAPI.ApiResponse<Issue>>> call)
    {
        IsSaving.Value = true;
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
            var response = await call(api).FirstAsync(cts.Token);
            var code = (int)(response.StatusCode ?? 0);
            if (code is < 200 or >= 300)
            {
                ErrorMessage.Value = code switch
                {
                    403 => "Not allowed to change assignees.",
                    422 => requesting
                        ? "GitHub rejected that assignee login."
                        : "GitHub could not remove that assignee.",
                    _ => $"Assignee update failed: {code}.",
                };
                return;
            }

            if (requesting)
                AssigneeLogin.Value = string.Empty;
            ApplyAssignees(response.Content?.Assignees);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Assignee update failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    private void ApplyAssignees(User[]? assignees)
    {
        Assignees.Clear();
        foreach (var user in assignees ?? [])
            Assignees.Add(user);
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
        AssigneeLogin.Dispose();
    }
}
