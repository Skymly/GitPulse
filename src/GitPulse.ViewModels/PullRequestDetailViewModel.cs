using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Pull request Conversation shell. Lifecycle, review, and metadata are
/// composites; this type keeps the page bindable surface and load/comment/edit.
/// </summary>
public sealed partial class PullRequestDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly PullRequestConversationIo _io;
    private readonly PullRequestLifecycle _lifecycle;
    private readonly PullRequestReviewComposer _review;
    private readonly PullRequestConversationMeta _meta;

    /// <summary>The pull request being viewed.</summary>
    public BindableReactiveProperty<PullRequest?> PullRequest { get; } = new(null);

    /// <summary>Conversation comments on the PR.</summary>
    public ObservableCollection<Comment> Comments { get; } = [];

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Whether a write operation (comment/state/merge/title-body) is in progress.</summary>
    public BindableReactiveProperty<bool> IsSaving { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>PR title for the page header.</summary>
    public BindableReactiveProperty<string> Title { get; } = new(string.Empty);

    /// <summary>Editable PR title draft (seeded on load).</summary>
    public BindableReactiveProperty<string> TitleInput { get; } = new(string.Empty);

    /// <summary>Editable PR body draft (seeded on load); empty body allowed on save.</summary>
    public BindableReactiveProperty<string> BodyInput { get; } = new(string.Empty);

    /// <summary>Repository owner (set by Initialize, used by Files tab).</summary>
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);

    /// <summary>Repository name (set by Initialize, used by Files tab).</summary>
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);

    /// <summary>Comment input text (two-way bound to editor).</summary>
    public BindableReactiveProperty<string> CommentInput { get; } = new(string.Empty);

    public BindableReactiveProperty<string> MergeMethod => _lifecycle.MergeMethod;
    public BindableReactiveProperty<bool> CanMerge => _lifecycle.CanMerge;
    public BindableReactiveProperty<string> MergeStatus => _lifecycle.MergeStatus;
    public BindableReactiveProperty<bool> IsMerged => _lifecycle.IsMerged;
    public BindableReactiveProperty<bool> CanUpdateBranch => _lifecycle.CanUpdateBranch;
    public BindableReactiveProperty<bool> IsUpdatingBranch => _lifecycle.IsUpdatingBranch;
    public BindableReactiveProperty<bool> CanMarkReadyForReview => _lifecycle.CanMarkReadyForReview;
    public BindableReactiveProperty<bool> IsMarkingReadyForReview => _lifecycle.IsMarkingReadyForReview;
    public BindableReactiveProperty<bool> CanConvertToDraft => _lifecycle.CanConvertToDraft;
    public BindableReactiveProperty<bool> IsConvertingToDraft => _lifecycle.IsConvertingToDraft;

    public ObservableCollection<PullRequestReview> Reviews => _review.Reviews;
    public BindableReactiveProperty<string> ReviewEvent => _review.ReviewEvent;
    public BindableReactiveProperty<string> ReviewBody => _review.ReviewBody;
    public BindableReactiveProperty<string> ViewerLogin => _review.ViewerLogin;
    public BindableReactiveProperty<bool> CanReview => _review.CanReview;
    public BindableReactiveProperty<bool> CanApproveOrRequestChanges => _review.CanApproveOrRequestChanges;
    public ObservableCollection<string> ReviewEventOptions => _review.ReviewEventOptions;

    public ObservableCollection<User> Assignees => _meta.Assignees;
    public ObservableCollection<Label> Labels => _meta.Labels;
    public ObservableCollection<User> RequestedReviewers => _meta.RequestedReviewers;
    public ObservableCollection<Team> RequestedTeams => _meta.RequestedTeams;
    public BindableReactiveProperty<string> ReviewerLogin => _meta.ReviewerLogin;
    public BindableReactiveProperty<string> AssigneeLogin => _meta.AssigneeLogin;
    public BindableReactiveProperty<string> LabelInput => _meta.LabelInput;
    public BindableReactiveProperty<bool> CanManageReviewers => _meta.CanManageReviewers;
    public BindableReactiveProperty<bool> HasRequestedReviewers => _meta.HasRequestedReviewers;

    public ObservableCollection<CheckRun> CheckRuns { get; } = [];
    public ObservableCollection<CommitStatus> CommitStatuses { get; } = [];
    public BindableReactiveProperty<string> GateRollup { get; } = new("No checks");

    public PullRequestDetailViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
        _io = new PullRequestConversationIo(clientFactory, ErrorMessage);
        _meta = new PullRequestConversationMeta(_io, PullRequest, IsSaving);
        _review = new PullRequestReviewComposer(
            _io, PullRequest, IsSaving, ApplyPullRequest, _meta.LoadRequestedAsync);
        _lifecycle = new PullRequestLifecycle(
            _io, PullRequest, IsSaving, ApplyPullRequest, _review.SyncPermissions);
    }

    [RelayCommand]
    private async Task OpenInBrowserAsync(string url)
    {
        if (!string.IsNullOrEmpty(url))
            await _browserLauncher.OpenAsync(url);
    }

    public void Initialize(string owner, string repo, int prNumber)
    {
        _io.Owner = owner;
        _io.Repo = repo;
        _io.Number = prNumber;
        Owner.Value = owner;
        RepoName.Value = repo;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_io.Owner) || string.IsNullOrEmpty(_io.Repo) || _io.Number <= 0)
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

            var pr = await api.GetPullRequest(_io.Owner, _io.Repo, _io.Number).FirstAsync(cts.Token);
            ApplyPullRequest(pr);

            var comments = await api.ListIssueComments(_io.Owner, _io.Repo, _io.Number).FirstAsync(cts.Token);
            Comments.Clear();
            foreach (var comment in comments)
                Comments.Add(comment);

            await _review.LoadAsync(api, cts.Token);
            await _meta.LoadRequestedAsync(api, cts.Token);
            await LoadGateAsync(api, cts.Token);
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
            var comment = await api.CreateIssueComment(_io.Owner, _io.Repo, _io.Number, request)
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

    [RelayCommand]
    private Task ToggleStateAsync() => _lifecycle.ToggleStateAsync();

    [RelayCommand]
    private async Task SaveTitleBodyAsync()
    {
        if (PullRequest.Value is null
            || string.IsNullOrWhiteSpace(TitleInput.Value)
            || IsSaving.Value)
        {
            return;
        }

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var (api, cts) = await _io.OpenAsync();
            if (api is null || cts is null)
                return;

            using (cts)
            {
                var request = new IssueUpdateRequest
                {
                    Title = TitleInput.Value.Trim(),
                    Body = BodyInput.Value,
                };
                await api.UpdateIssue(_io.Owner, _io.Repo, _io.Number, request).FirstAsync(cts.Token);

                var pr = await api.GetPullRequest(_io.Owner, _io.Repo, _io.Number).FirstAsync(cts.Token);
                ApplyPullRequest(pr);
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    [RelayCommand]
    private Task RequestReviewerAsync() => _meta.RequestReviewerAsync();

    [RelayCommand]
    private Task RemoveRequestedReviewerAsync(string? login) => _meta.RemoveRequestedReviewerAsync(login);

    [RelayCommand]
    private Task SubmitReviewAsync() => _review.SubmitAsync();

    [RelayCommand]
    private Task MergeAsync() => _lifecycle.MergeAsync();

    [RelayCommand]
    private Task UpdateBranchAsync() => _lifecycle.UpdateBranchAsync();

    [RelayCommand]
    private Task MarkReadyForReviewAsync() => _lifecycle.MarkReadyForReviewAsync();

    [RelayCommand]
    private Task ConvertToDraftAsync() => _lifecycle.ConvertToDraftAsync();

    [RelayCommand]
    private Task AddAssigneeAsync() => _meta.AddAssigneeAsync();

    [RelayCommand]
    private Task RemoveAssigneeAsync(string? login) => _meta.RemoveAssigneeAsync(login);

    [RelayCommand]
    private Task SaveLabelsAsync() => _meta.SaveLabelsAsync();

    public void Dispose()
    {
        _lifecycle.Dispose();
        _review.Dispose();
        _meta.Dispose();
        PullRequest.Dispose();
        IsLoading.Dispose();
        IsSaving.Dispose();
        ErrorMessage.Dispose();
        Title.Dispose();
        TitleInput.Dispose();
        BodyInput.Dispose();
        Owner.Dispose();
        RepoName.Dispose();
        CommentInput.Dispose();
        GateRollup.Dispose();
    }

    private void ApplyPullRequest(PullRequest pr)
    {
        PullRequest.Value = pr;
        Title.Value = $"#{pr.Number} {pr.Title}";
        TitleInput.Value = pr.Title;
        BodyInput.Value = pr.Body ?? string.Empty;
        _lifecycle.Sync(pr);
        _review.SyncPermissions(pr);
        _meta.Sync(pr);
    }

    private async Task LoadGateAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        var state = await HeadGateRollup.LoadAsync(
            api, _io.Owner, _io.Repo, PullRequest.Value?.Head?.Sha, cancellationToken);
        CheckRuns.Clear();
        foreach (var run in state.Runs)
            CheckRuns.Add(run);
        CommitStatuses.Clear();
        foreach (var status in state.Statuses)
            CommitStatuses.Add(status);
        GateRollup.Value = state.Summary;
    }
}

