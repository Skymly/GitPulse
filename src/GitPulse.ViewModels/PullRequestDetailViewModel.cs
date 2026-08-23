using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Pull request detail view model — shows a single PR and its conversation
/// comments. Demonstrates <see cref="IGitHubReposApi.GetPullRequest"/>,
/// <see cref="IGitHubReposApi.ListIssueComments"/> (PR conversation comments
/// share the issue comments endpoint), and M3 CRUD operations:
/// <see cref="IGitHubReposApi.CreateIssueComment"/> (PR comments use the
/// issue comments endpoint) and <see cref="IGitHubReposApi.UpdateIssue"/>
/// (PR state toggle and title/body edit via the issue PATCH endpoint).
/// M6 adds PR merge via <see cref="IGitHubReposApi.MergePullRequest"/>.
/// M15 adds Pull Request Review list/submit via
/// <see cref="IGitHubReposApi.ListPullRequestReviews"/> and
/// <see cref="IGitHubReposApi.CreatePullRequestReview"/>.
/// M16 adds a PR-head Gate Rollup via
/// <see cref="IGitHubReposApi.ListCheckRunsForRef"/> and
/// <see cref="IGitHubReposApi.GetCombinedStatusForRef"/>.
/// M21 adds pending review requests via
/// <see cref="IGitHubReposApi.ListRequestedReviewers"/>,
/// <see cref="IGitHubReposApi.RequestReviewers"/>, and
/// <see cref="IGitHubReposApi.RemoveRequestedReviewers"/>.
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

    // ── M6: Merge state ──────────────────────────────────────────

    /// <summary>Selected merge method: "merge", "squash", or "rebase".</summary>
    public BindableReactiveProperty<string> MergeMethod { get; } = new("merge");

    /// <summary>Whether the merge button is enabled (PR is open, mergeable, not draft).</summary>
    public BindableReactiveProperty<bool> CanMerge { get; } = new(false);

    /// <summary>Status text for mergeability (e.g. "Mergeable", "Conflicts", "Pending").</summary>
    public BindableReactiveProperty<string> MergeStatus { get; } = new(string.Empty);

    /// <summary>Whether the PR has been merged (shows merge result instead of merge button).</summary>
    public BindableReactiveProperty<bool> IsMerged { get; } = new(false);

    /// <summary>Submitted Pull Request Reviews (PENDING omitted).</summary>
    public ObservableCollection<PullRequestReview> Reviews { get; } = [];

    /// <summary>Users currently requested to review (not yet submitted).</summary>
    public ObservableCollection<User> Assignees { get; } = [];

    public ObservableCollection<User> RequestedReviewers { get; } = [];

    /// <summary>Teams currently requested to review (display-only).</summary>
    public ObservableCollection<Team> RequestedTeams { get; } = [];

    /// <summary>Login typed when requesting a reviewer.</summary>
    public BindableReactiveProperty<string> ReviewerLogin { get; } = new(string.Empty);

    /// <summary>GitHub login to assign.</summary>
    public BindableReactiveProperty<string> AssigneeLogin { get; } = new(string.Empty);

    /// <summary>True when the PR is open and not merged.</summary>
    public BindableReactiveProperty<bool> CanManageReviewers { get; } = new(false);

    /// <summary>True when at least one user or team is requested.</summary>
    public BindableReactiveProperty<bool> HasRequestedReviewers { get; } = new(false);

    /// <summary>Review Event for submit: APPROVE, REQUEST_CHANGES, or COMMENT.</summary>
    public BindableReactiveProperty<string> ReviewEvent { get; } = new("COMMENT");

    /// <summary>Summary body for the Pull Request Review being submitted.</summary>
    public BindableReactiveProperty<string> ReviewBody { get; } = new(string.Empty);

    /// <summary>Authenticated login; empty when GET /user failed.</summary>
    public BindableReactiveProperty<string> ViewerLogin { get; } = new(string.Empty);

    /// <summary>True when the PR is open and not merged.</summary>
    public BindableReactiveProperty<bool> CanReview { get; } = new(false);

    /// <summary>True when the viewer is not the PR author (or viewer is unknown).</summary>
    public BindableReactiveProperty<bool> CanApproveOrRequestChanges { get; } = new(true);

    /// <summary>Review Event picker options; authors only get COMMENT.</summary>
    public ObservableCollection<string> ReviewEventOptions { get; } = ["COMMENT", "APPROVE", "REQUEST_CHANGES"];

    /// <summary>Latest Check Runs for the PR head SHA.</summary>
    public ObservableCollection<CheckRun> CheckRuns { get; } = [];

    /// <summary>Combined Commit Statuses for the PR head SHA.</summary>
    public ObservableCollection<CommitStatus> CommitStatuses { get; } = [];

    /// <summary>Client Gate Rollup: Pending, Success, Failure, or No checks.</summary>
    public BindableReactiveProperty<string> GateRollup { get; } = new("No checks");

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
        Owner.Value = owner;
        RepoName.Value = repo;
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

            var pr = await api.GetPullRequest(_owner, _repo, _prNumber).FirstAsync(cts.Token);
            ApplyPullRequest(pr);

            var comments = await api.ListIssueComments(_owner, _repo, _prNumber).FirstAsync(cts.Token);
            Comments.Clear();
            foreach (var comment in comments)
                Comments.Add(comment);

            await LoadReviewExtrasAsync(api, cts.Token);
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

    /// <summary>Post a new conversation comment on the PR.</summary>
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
            var comment = await api.CreateIssueComment(_owner, _repo, _prNumber, request)
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

    /// <summary>Toggle the PR state between "open" and "closed" (via issue PATCH endpoint).</summary>
    [RelayCommand]
    private async Task ToggleStateAsync()
    {
        if (PullRequest.Value is null || IsSaving.Value)
            return;

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var newState = PullRequest.Value.State == "open" ? "closed" : "open";
            var request = new IssueUpdateRequest { State = newState };
            // PR state is toggled via the issue PATCH endpoint (GitHub REST API).
            await api.UpdateIssue(_owner, _repo, _prNumber, request).FirstAsync(cts.Token);

            // Update the local PR state (PATCH returns an Issue, not a PullRequest).
            var pr = PullRequest.Value;
            PullRequest.Value = new PullRequest
            {
                Number = pr.Number,
                Title = pr.Title,
                Body = pr.Body,
                State = newState,
                Draft = pr.Draft,
                Merged = pr.Merged,
                HtmlUrl = pr.HtmlUrl,
                CreatedAt = pr.CreatedAt,
                UpdatedAt = pr.UpdatedAt,
                User = pr.User,
                MergedBy = pr.MergedBy,
                HeadRef = pr.HeadRef,
                BaseRef = pr.BaseRef,
            };
            UpdateReviewPermissions(PullRequest.Value);
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

    /// <summary>
    /// Save PR title and body via the issue PATCH endpoint, then refresh detail
    /// with <see cref="IGitHubReposApi.GetPullRequest"/>. Empty title is rejected;
    /// empty body is allowed.
    /// </summary>
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
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var request = new IssueUpdateRequest
            {
                Title = TitleInput.Value.Trim(),
                Body = BodyInput.Value,
            };
            await api.UpdateIssue(_owner, _repo, _prNumber, request).FirstAsync(cts.Token);

            var pr = await api.GetPullRequest(_owner, _repo, _prNumber).FirstAsync(cts.Token);
            ApplyPullRequest(pr);
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

    private void ApplyPullRequest(PullRequest pr)
    {
        PullRequest.Value = pr;
        Title.Value = $"#{pr.Number} {pr.Title}";
        TitleInput.Value = pr.Title;
        BodyInput.Value = pr.Body ?? string.Empty;
        UpdateMergeStatus(pr);
        UpdateReviewPermissions(pr);
        CanManageReviewers.Value = pr.State == "open" && !pr.Merged;
        ApplyAssignees(pr.Assignees);
    }

    private void UpdateReviewPermissions(PullRequest pr)
    {
        CanReview.Value = pr.State == "open" && !pr.Merged;
        var author = pr.User?.Login;
        CanApproveOrRequestChanges.Value = string.IsNullOrEmpty(ViewerLogin.Value)
            || string.IsNullOrEmpty(author)
            || !string.Equals(ViewerLogin.Value, author, StringComparison.OrdinalIgnoreCase);

        ReviewEventOptions.Clear();
        ReviewEventOptions.Add("COMMENT");
        if (CanApproveOrRequestChanges.Value)
        {
            ReviewEventOptions.Add("APPROVE");
            ReviewEventOptions.Add("REQUEST_CHANGES");
        }

        if (!ReviewEventOptions.Any(option =>
                string.Equals(option, ReviewEvent.Value, StringComparison.OrdinalIgnoreCase)))
            ReviewEvent.Value = "COMMENT";
    }

    private async Task LoadReviewExtrasAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        try
        {
            var user = await api.GetAuthenticatedUser().FirstAsync(cancellationToken);
            ViewerLogin.Value = user.Login ?? string.Empty;
        }
        catch
        {
            ViewerLogin.Value = string.Empty;
        }

        if (PullRequest.Value is not null)
            UpdateReviewPermissions(PullRequest.Value);

        try
        {
            var reviews = await api.ListPullRequestReviews(_owner, _repo, _prNumber)
                .FirstAsync(cancellationToken);
            ReplaceReviews(reviews);
        }
        catch
        {
            Reviews.Clear();
        }

        await LoadRequestedReviewersAsync(api, cancellationToken);
    }

    private async Task LoadRequestedReviewersAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        try
        {
            var requested = await api.ListRequestedReviewers(_owner, _repo, _prNumber)
                .FirstAsync(cancellationToken);
            ReplaceRequestedReviewers(requested);
        }
        catch
        {
            RequestedReviewers.Clear();
            RequestedTeams.Clear();
            HasRequestedReviewers.Value = false;
        }
    }

    private void ReplaceRequestedReviewers(RequestedReviewers requested)
    {
        RequestedReviewers.Clear();
        foreach (var user in requested.Users ?? [])
            RequestedReviewers.Add(user);

        RequestedTeams.Clear();
        foreach (var team in requested.Teams ?? [])
            RequestedTeams.Add(team);

        HasRequestedReviewers.Value = RequestedReviewers.Count > 0 || RequestedTeams.Count > 0;
    }

    private async Task LoadGateAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        var sha = PullRequest.Value?.Head?.Sha;
        if (string.IsNullOrEmpty(sha))
        {
            CheckRuns.Clear();
            CommitStatuses.Clear();
            GateRollup.Value = "No checks";
            return;
        }

        CheckRun[] runs = [];
        CombinedCommitStatus? combined = null;

        try
        {
            var result = await api.ListCheckRunsForRef(_owner, _repo, sha, "latest")
                .FirstAsync(cancellationToken);
            runs = result.CheckRuns ?? [];
            ReplaceCheckRuns(runs);
        }
        catch
        {
            CheckRuns.Clear();
            runs = [];
        }

        try
        {
            combined = await api.GetCombinedStatusForRef(_owner, _repo, sha)
                .FirstAsync(cancellationToken);
            ReplaceCommitStatuses(combined.Statuses ?? []);
        }
        catch
        {
            CommitStatuses.Clear();
            combined = null;
        }

        GateRollup.Value = ComputeGateRollup(runs, combined);
    }

    private void ReplaceCheckRuns(IEnumerable<CheckRun> runs)
    {
        CheckRuns.Clear();
        foreach (var run in runs)
            CheckRuns.Add(run);
    }

    private void ReplaceCommitStatuses(IEnumerable<CommitStatus> statuses)
    {
        CommitStatuses.Clear();
        foreach (var status in statuses)
            CommitStatuses.Add(status);
    }

    internal static string ComputeGateRollup(
        IReadOnlyList<CheckRun> runs, CombinedCommitStatus? combined)
    {
        var statuses = combined?.Statuses ?? [];
        if (runs.Count == 0 && statuses.Length == 0)
            return "No checks";

        if (runs.Any(IsIncompleteCheckRun) ||
            statuses.Any(status => status.State.Equals("pending", StringComparison.OrdinalIgnoreCase)))
            return "Pending";

        if (runs.Any(IsFailedCheckRun) ||
            statuses.Any(status =>
                status.State.Equals("failure", StringComparison.OrdinalIgnoreCase) ||
                status.State.Equals("error", StringComparison.OrdinalIgnoreCase)))
            return "Failure";

        return "Success";
    }

    private static bool IsIncompleteCheckRun(CheckRun run) =>
        !run.Status.Equals("completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailedCheckRun(CheckRun run)
    {
        var conclusion = run.Conclusion;
        return conclusion is not null &&
               (conclusion.Equals("failure", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("timed_out", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("cancelled", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("startup_failure", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("action_required", StringComparison.OrdinalIgnoreCase));
    }

    private void ReplaceReviews(IEnumerable<PullRequestReview> reviews)
    {
        Reviews.Clear();
        foreach (var review in reviews)
        {
            if (string.Equals(review.State, "PENDING", StringComparison.OrdinalIgnoreCase))
                continue;
            Reviews.Add(review);
        }
    }


    /// <summary>Request a review from the typed user login.</summary>
    [RelayCommand]
    private async Task RequestReviewerAsync()
    {
        var login = ReviewerLogin.Value.Trim().TrimStart('@');
        if (login.Length == 0 || PullRequest.Value is null || IsSaving.Value || !CanManageReviewers.Value)
            return;

        if (RequestedReviewers.Any(user =>
                string.Equals(user.Login, login, StringComparison.OrdinalIgnoreCase)))
            return;

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
            var request = new ReviewersRequest { Reviewers = [login] };
            var response = await api.RequestReviewers(_owner, _repo, _prNumber, request)
                .FirstAsync(cts.Token);
            if (!ApplyReviewerWriteStatus(response.StatusCode, requesting: true))
                return;

            ReviewerLogin.Value = string.Empty;
            await LoadRequestedReviewersAsync(api, cts.Token);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Request reviewer failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    /// <summary>Remove a pending requested user.</summary>
    [RelayCommand]
    private async Task RemoveRequestedReviewerAsync(string? login)
    {
        login = login?.Trim();
        if (string.IsNullOrEmpty(login) || PullRequest.Value is null || IsSaving.Value || !CanManageReviewers.Value)
            return;

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
            var request = new ReviewersRequest { Reviewers = [login] };
            var response = await api.RemoveRequestedReviewers(_owner, _repo, _prNumber, request)
                .FirstAsync(cts.Token);
            if (!ApplyReviewerWriteStatus(response.StatusCode, requesting: false))
                return;

            await LoadRequestedReviewersAsync(api, cts.Token);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Remove reviewer failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    private bool ApplyReviewerWriteStatus(HttpStatusCode? status, bool requesting)
    {
        if (status is null || ((int)status >= 200 && (int)status < 300))
            return true;

        ErrorMessage.Value = status switch
        {
            HttpStatusCode.UnprocessableEntity =>
                "GitHub rejected that reviewer. They may not be a collaborator.",
            HttpStatusCode.Forbidden => "Not allowed to change review requests.",
            _ => requesting
                ? $"Request reviewer failed: {(int)status}."
                : $"Remove reviewer failed: {(int)status}.",
        };
        return false;
    }
    /// <summary>Submit a Pull Request Review with the selected Review Event.</summary>
    [RelayCommand]
    private async Task SubmitReviewAsync()
    {
        if (PullRequest.Value is null || IsSaving.Value || !CanReview.Value)
            return;

        var reviewEvent = ReviewEvent.Value;
        if (string.IsNullOrWhiteSpace(reviewEvent))
            return;

        var isApprove = string.Equals(reviewEvent, "APPROVE", StringComparison.OrdinalIgnoreCase);
        var isRequestChanges = string.Equals(reviewEvent, "REQUEST_CHANGES", StringComparison.OrdinalIgnoreCase);
        if ((isApprove || isRequestChanges) && !CanApproveOrRequestChanges.Value)
            return;

        if (!isApprove && string.IsNullOrWhiteSpace(ReviewBody.Value))
            return;

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

            var request = new PullRequestReviewCreateRequest
            {
                Event = reviewEvent,
                Body = string.IsNullOrWhiteSpace(ReviewBody.Value) ? null : ReviewBody.Value,
                CommitId = PullRequest.Value.Head?.Sha,
            };
            await api.CreatePullRequestReview(_owner, _repo, _prNumber, request)
                .FirstAsync(cts.Token);

            ReviewBody.Value = string.Empty;

            try
            {
                var reviews = await api.ListPullRequestReviews(_owner, _repo, _prNumber)
                    .FirstAsync(cts.Token);
                ReplaceReviews(reviews);
            }
            catch
            {
                // Keep the local list; submit already succeeded.
            }

            await LoadRequestedReviewersAsync(api, cts.Token);

            try
            {
                var pr = await api.GetPullRequest(_owner, _repo, _prNumber).FirstAsync(cts.Token);
                ApplyPullRequest(pr);
            }
            catch
            {
                // Submit succeeded; mergeable refresh is best-effort.
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Review failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    // ── M6: Merge logic ──────────────────────────────────────────

    /// <summary>
    /// Update merge-related reactive state from the PR model. Called after
    /// load and after merge operations.
    /// </summary>
    private void UpdateMergeStatus(PullRequest pr)
    {
        IsMerged.Value = pr.Merged;

        if (pr.Merged)
        {
            CanMerge.Value = false;
            MergeStatus.Value = "Merged";
            return;
        }

        if (pr.State != "open")
        {
            CanMerge.Value = false;
            MergeStatus.Value = "Closed";
            return;
        }

        if (pr.Draft)
        {
            CanMerge.Value = false;
            MergeStatus.Value = "Draft — needs to be marked ready for review";
            return;
        }

        // Mergeable can be null while GitHub computes it.
        CanMerge.Value = pr.Mergeable ?? false;
        MergeStatus.Value = pr.Mergeable switch
        {
            true => pr.MergeableState == "clean" ? "Mergeable" : $"Mergeable ({pr.MergeableState})",
            false => "Conflicts — cannot merge",
            null => "Checking mergeability...",
        };
    }

    /// <summary>Merge the pull request using the selected merge method.</summary>
    [RelayCommand]
    private async Task MergeAsync()
    {
        if (PullRequest.Value is null || IsSaving.Value || !CanMerge.Value)
            return;

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

            var request = new MergeRequest
            {
                Method = MergeMethod.Value,
                CommitTitle = $"Merge #{PullRequest.Value.Number} {PullRequest.Value.Title}",
            };

            var response = await api.MergePullRequest(_owner, _repo, _prNumber, request)
                .FirstAsync(cts.Token);

            if (response.Merged)
            {
                // Update the PR to reflect merged state.
                var pr = PullRequest.Value;
                PullRequest.Value = new PullRequest
                {
                    Number = pr.Number,
                    Title = pr.Title,
                    Body = pr.Body,
                    State = "closed",
                    Draft = pr.Draft,
                    Merged = true,
                    HtmlUrl = pr.HtmlUrl,
                    CreatedAt = pr.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    User = pr.User,
                    MergedBy = pr.User,
                    HeadRef = pr.HeadRef,
                    BaseRef = pr.BaseRef,
                    Mergeable = false,
                    MergeableState = pr.MergeableState,
                    MergeCommitSha = response.Sha,
                    Commits = pr.Commits,
                    Additions = pr.Additions,
                    Deletions = pr.Deletions,
                    ChangedFiles = pr.ChangedFiles,
                };
                UpdateMergeStatus(PullRequest.Value);
                UpdateReviewPermissions(PullRequest.Value);
            }
            else
            {
                ErrorMessage.Value = response.Message;
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Merge failed: {ex.Message}";
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
        if (login.Length == 0 || PullRequest.Value is null || IsSaving.Value || !CanManageReviewers.Value)
            return;

        if (Assignees.Any(user =>
                string.Equals(user.Login, login, StringComparison.OrdinalIgnoreCase)))
            return;

        await WriteAssigneesAsync(
            requesting: true,
            api => api.AddIssueAssignees(
                _owner, _repo, _prNumber, new AssigneesRequest { Assignees = [login] }));
    }

    /// <summary>Remove an assignee by login.</summary>
    [RelayCommand]
    private async Task RemoveAssigneeAsync(string? login)
    {
        login = login?.Trim();
        if (string.IsNullOrEmpty(login) || PullRequest.Value is null || IsSaving.Value || !CanManageReviewers.Value)
            return;

        await WriteAssigneesAsync(
            requesting: false,
            api => api.RemoveIssueAssignees(
                _owner, _repo, _prNumber, new AssigneesRequest { Assignees = [login] }));
    }

    private async Task WriteAssigneesAsync(
        bool requesting,
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
        MergeMethod.Dispose();
        CanMerge.Dispose();
        MergeStatus.Dispose();
        IsMerged.Dispose();
        ReviewEvent.Dispose();
        ReviewBody.Dispose();
        ViewerLogin.Dispose();
        CanReview.Dispose();
        CanApproveOrRequestChanges.Dispose();
        ReviewerLogin.Dispose();
        CanManageReviewers.Dispose();
        HasRequestedReviewers.Dispose();
        AssigneeLogin.Dispose();
        GateRollup.Dispose();
    }
}


