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

    [Get("/user/repos")]
    Observable<ApiResponse<Repo[]>> ListMyReposPaged();

    [Get("/repos/{owner}/{repo}")]
    Observable<Repo> GetRepo(string owner, string repo);

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

    // ── PR Merge (M6: PR review & merge) ─────────────────────────

    /// <summary>
    /// Merge a pull request. The <see cref="MergeRequest.Method"/> field
    /// selects merge, squash, or rebase. Returns the merge commit SHA.
    /// </summary>
    [Put("/repos/{owner}/{repo}/pulls/{number}/merge")]
    Observable<MergeResponse> MergePullRequest(
        string owner, string repo, int number, [Body] MergeRequest body);

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
