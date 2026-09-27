using GitPulse.Core.Models;
using Observables.RestAPI;
using R3;

namespace GitPulse.GitHubApi;

/// <summary>
/// Declarative GitHub Actions REST API (workflow runs, jobs, rerun, dispatch).
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
    Observable<ApiResponse<GitHubNoContent>> RerunWorkflow(string owner, string repo, long runId);

    /// <summary>List repository workflows (M40).</summary>
    [Get("/repos/{owner}/{repo}/actions/workflows")]
    Observable<WorkflowsResult> ListWorkflows(string owner, string repo);

    /// <summary>Create a workflow_dispatch event (204) (M40).</summary>
    [Post("/repos/{owner}/{repo}/actions/workflows/{workflowId}/dispatches")]
    Observable<ApiResponse<GitHubNoContent>> DispatchWorkflow(
        string owner, string repo, long workflowId, [Body] WorkflowDispatchRequest body);
}
