using GitPulse.Core.Models;
using Observables.RestAPI;
using R3;

namespace GitPulse.GitHubApi;

/// <summary>
/// Declarative GitHub Actions REST API (workflow runs, jobs, rerun, logs).
/// Pagination for list endpoints is injected by <c>GitHubQueryHandler</c>.
/// </summary>
public interface IGitHubActionsApi
{
    [Get("/repos/{owner}/{repo}/actions/runs")]
    Observable<ApiResponse<WorkflowRunsResult>> ListWorkflowRuns(string owner, string repo);

    [Get("/repos/{owner}/{repo}/actions/runs/{runId}")]
    Observable<WorkflowRun> GetWorkflowRun(string owner, string repo, long runId);

    [Get("/repos/{owner}/{repo}/actions/runs/{runId}/jobs")]
    Observable<ApiResponse<WorkflowJobsResult>> ListWorkflowJobs(string owner, string repo, long runId);

    [Post("/repos/{owner}/{repo}/actions/runs/{runId}/rerun")]
    Observable<Unit> RerunWorkflow(string owner, string repo, long runId);

    /// <summary>
    /// Returns a redirect to a short-lived plain-text log download URL.
    /// Callers should follow redirects or read the Location header.
    /// </summary>
    [Get("/repos/{owner}/{repo}/actions/jobs/{jobId}/logs")]
    Observable<ApiResponse<string>> GetJobLogs(string owner, string repo, long jobId);
}
