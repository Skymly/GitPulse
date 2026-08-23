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

    /// <summary>List repository workflows (M40).</summary>
    [Get("/repos/{owner}/{repo}/actions/workflows")]
    Observable<WorkflowsResult> ListWorkflows(string owner, string repo);

    /// <summary>Create a workflow_dispatch event (204) (M40).</summary>
    [Post("/repos/{owner}/{repo}/actions/workflows/{workflowId}/dispatches")]
    Observable<ApiResponse<Unit>> DispatchWorkflow(
        string owner, string repo, long workflowId, [Body] WorkflowDispatchRequest body);

    /// <summary>
    /// Returns a redirect to a short-lived plain-text log download URL.
    /// Callers should follow redirects or read the Location header.
    /// </summary>
    [Get("/repos/{owner}/{repo}/actions/jobs/{jobId}/logs")]
    Observable<ApiResponse<string>> GetJobLogs(string owner, string repo, long jobId);
}
