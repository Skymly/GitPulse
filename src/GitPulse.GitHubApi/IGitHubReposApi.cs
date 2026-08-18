using GitPulse.Core.Models;
using Observables.RestAPI;
using R3;

namespace GitPulse.GitHubApi;

/// <summary>
/// Declarative GitHub Repos REST API.
/// The Observables.RestAPI.R3 source generator produces an HttpClient proxy
/// implementation at compile time; consume via <c>RestService.For&lt;IGitHubReposApi&gt;(httpClient)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observables 0.1.5:</b> <c>ValidatePathTemplate</c> now runs after parameter
/// classification, so <c>[Body]</c>/<c>[Query]</c>/<c>[Header]</c> parameters are
/// correctly excluded from path-template validation. Path + body parameters can
/// coexist on the same method. See
/// <see href="https://github.com/Skymly/Observables/issues/111"/>.
/// </para>
/// <para>
/// <b>Pagination via <c>ApiResponse&lt;T&gt;</c>:</b> List methods that need the
/// <c>Link</c> response header (for <c>rel="next"</c> pagination detection) return
/// <c>Observable&lt;ApiResponse&lt;T&gt;&gt;</c> instead of <c>Observable&lt;T&gt;</c>.
/// The <c>ApiResponse&lt;T&gt;</c> wrapper exposes <c>Headers</c> (including <c>Link</c>)
/// alongside the deserialized <c>Content</c>. The page number is controlled by the
/// <c>GitHubQueryHandler</c> which injects <c>page</c>/<c>per_page</c> query parameters
/// into outgoing requests.
/// </para>
/// </remarks>
public interface IGitHubReposApi
{
    // ── Repositories ──────────────────────────────────────────────

    /// <summary>
    /// Authenticated user (M15). <see cref="User.Login"/> is compared to the
    /// PR author so self APPROVE / REQUEST_CHANGES can be disabled.
    /// </summary>
    [Get("/user")]
    Observable<User> GetAuthenticatedUser();

    [Get("/user/repos")]
    Observable<ApiResponse<Repo[]>> ListMyReposPaged();

    [Get("/repos/{owner}/{repo}")]
    Observable<Repo> GetRepo(string owner, string repo);

    // ── Repository detail (M7: repo detail page) ──────────────────

    /// <summary>
    /// Get the preferred README for a repository. Returns a
    /// <see cref="FileContent"/> with base64-encoded <see cref="FileContent.Content"/>.
    /// Returns 404 when no README exists.
    /// </summary>
    [Get("/repos/{owner}/{repo}/readme")]
    Observable<FileContent> GetReadme(string owner, string repo);

    /// <summary>List branches for a repository.</summary>
    [Get("/repos/{owner}/{repo}/branches")]
    Observable<Branch[]> ListBranches(string owner, string repo);

    /// <summary>List releases for a repository. <see cref="Release.Body"/> contains release notes as Markdown.</summary>
    [Get("/repos/{owner}/{repo}/releases")]
    Observable<Release[]> ListReleases(string owner, string repo);

    // ── Issues ────────────────────────────────────────────────────

    [Get("/repos/{owner}/{repo}/issues")]
    Observable<ApiResponse<Issue[]>> ListIssuesPaged(string owner, string repo);

    [Get("/repos/{owner}/{repo}/issues/{number}")]
    Observable<Issue> GetIssue(string owner, string repo, int number);

    [Post("/repos/{owner}/{repo}/issues")]
    Observable<Issue> CreateIssue(string owner, string repo, [Body] IssueCreateRequest body);

    [Patch("/repos/{owner}/{repo}/issues/{number}")]
    Observable<Issue> UpdateIssue(string owner, string repo, int number, [Body] IssueUpdateRequest body);

    [Get("/repos/{owner}/{repo}/issues/{number}/comments")]
    Observable<Comment[]> ListIssueComments(string owner, string repo, int number);

    [Post("/repos/{owner}/{repo}/issues/{number}/comments")]
    Observable<Comment> CreateIssueComment(string owner, string repo, int number, [Body] CommentCreateRequest body);

    // ── Labels ────────────────────────────────────────────────────

    [Get("/repos/{owner}/{repo}/issues/{number}/labels")]
    Observable<Label[]> ListIssueLabels(string owner, string repo, int number);

    [Put("/repos/{owner}/{repo}/issues/{number}/labels")]
    Observable<Label[]> ReplaceIssueLabels(string owner, string repo, int number, [Body] LabelsReplaceRequest body);

    // ── Pull Requests ─────────────────────────────────────────────

    [Get("/repos/{owner}/{repo}/pulls")]
    Observable<ApiResponse<PullRequest[]>> ListPullRequestsPaged(string owner, string repo);

    [Get("/repos/{owner}/{repo}/pulls/{number}")]
    Observable<PullRequest> GetPullRequest(string owner, string repo, int number);

    [Post("/repos/{owner}/{repo}/pulls")]
    Observable<PullRequest> CreatePullRequest(
        string owner, string repo, [Body] PullRequestCreateRequest body);

    // ── PR Merge (M6: PR review & merge) ─────────────────────────

    /// <summary>
    /// Merge a pull request. The <see cref="MergeRequest.Method"/> field
    /// selects merge, squash, or rebase. Returns the merge commit SHA.
    /// </summary>
    [Put("/repos/{owner}/{repo}/pulls/{number}/merge")]
    Observable<MergeResponse> MergePullRequest(
        string owner, string repo, int number, [Body] MergeRequest body);

    // ── PR Diff (M8: diff viewer) ─────────────────────────────────

    /// <summary>List files changed in a pull request.</summary>
    [Get("/repos/{owner}/{repo}/pulls/{number}/files")]
    Observable<DiffEntry[]> ListPullRequestFiles(string owner, string repo, int number);

    /// <summary>List review comments on a pull request diff.</summary>
    [Get("/repos/{owner}/{repo}/pulls/{number}/comments")]
    Observable<ReviewComment[]> ListReviewComments(string owner, string repo, int number);

    /// <summary>Create a review comment on a specific line of the diff.</summary>
    [Post("/repos/{owner}/{repo}/pulls/{number}/comments")]
    Observable<ReviewComment> CreateReviewComment(
        string owner, string repo, int number, [Body] ReviewCommentRequest body);

    // ── Pull Request Reviews (M15: immediate submit) ──────────────

    /// <summary>
    /// List Pull Request Reviews (first page). Submitted <c>state</c> is not
    /// the create-time Review Event.
    /// </summary>
    [Get("/repos/{owner}/{repo}/pulls/{number}/reviews")]
    Observable<PullRequestReview[]> ListPullRequestReviews(string owner, string repo, int number);

    /// <summary>
    /// Submit a Pull Request Review immediately. Set
    /// <see cref="PullRequestReviewCreateRequest.Event"/> to APPROVE,
    /// REQUEST_CHANGES, or COMMENT (do not omit — that would be pending).
    /// </summary>
    [Post("/repos/{owner}/{repo}/pulls/{number}/reviews")]
    Observable<PullRequestReview> CreatePullRequestReview(
        string owner, string repo, int number, [Body] PullRequestReviewCreateRequest body);

    // ── Check Runs / Commit Statuses (M16: PR head Gate Rollup) ──

    /// <summary>
    /// List Check Runs for a git ref (PR head SHA). Pass
    /// <paramref name="filter"/> as <c>latest</c> for one row per name.
    /// First page only — not an <c>ApiResponse</c> wrapper.
    /// </summary>
    [Get("/repos/{owner}/{repo}/commits/{ref}/check-runs")]
    Observable<CheckRunsResult> ListCheckRunsForRef(
        string owner, string repo, string @ref, [Query] string filter);

    /// <summary>
    /// Combined Commit Statuses for a git ref. Does not include Check Runs.
    /// Empty <c>statuses</c> yields GitHub combined state <c>pending</c>.
    /// </summary>
    [Get("/repos/{owner}/{repo}/commits/{ref}/status")]
    Observable<CombinedCommitStatus> GetCombinedStatusForRef(
        string owner, string repo, string @ref);

    // ── Notifications ─────────────────────────────────────────────
    // M4: Notification center with polling-simulated realtime.
    // The poller (INotificationPoller) calls ListNotifications on a
    // timer (R3 Observable.Interval) and streams results to the UI.

    /// <summary>
    /// List all notifications for the authenticated user.
    /// Query params (all, participating) injected by GitHubQueryHandler.
    /// </summary>
    [Get("/notifications")]
    Observable<Notification[]> ListNotifications();

    /// <summary>Mark a single notification thread as read (DELETE).</summary>
    [Delete("/notifications/threads/{threadId}")]
    Observable<Unit> MarkThreadRead(string threadId);

    /// <summary>Mark all notifications as read (PUT).</summary>
    [Put("/notifications")]
    Observable<Unit> MarkAllRead();

    // ── Repository Contents (M5: File browsing & editing) ────────
    // The GitHub Contents API uses the same endpoint for directory listing
    // and file content — the response shape differs based on whether the
    // path points to a file or directory. We declare two methods with the
    // same path template but different return types; the caller chooses
    // based on the entry type from a prior directory listing.

    /// <summary>
    /// List directory contents. Returns an array of file/directory entries.
    /// Use an empty <paramref name="path"/> for the repository root.
    /// </summary>
    [Get("/repos/{owner}/{repo}/contents/{path}")]
    Observable<ContentEntry[]> ListContents(string owner, string repo, string path);

    /// <summary>
    /// Get file content (base64-encoded). Use when the path is known to
    /// point to a file.
    /// </summary>
    [Get("/repos/{owner}/{repo}/contents/{path}")]
    Observable<FileContent> GetFileContent(string owner, string repo, string path);

    /// <summary>
    /// Create or update a file. <see cref="FileUpdateRequest.Sha"/> is
    /// required for updates, omitted for creates.
    /// </summary>
    [Put("/repos/{owner}/{repo}/contents/{path}")]
    Observable<FileCommitResponse> CreateOrUpdateFile(
        string owner, string repo, string path, [Body] FileUpdateRequest body);

    /// <summary>Delete a file. Requires the file's current SHA.</summary>
    [Delete("/repos/{owner}/{repo}/contents/{path}")]
    Observable<FileCommitResponse> DeleteFile(
        string owner, string repo, string path, [Body] FileDeleteRequest body);
}
