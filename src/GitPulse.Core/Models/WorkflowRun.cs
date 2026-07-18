using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Wrapper for <c>GET /repos/{owner}/{repo}/actions/runs</c>.
/// </summary>
public sealed class WorkflowRunsResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("workflow_runs")]
    public WorkflowRun[] WorkflowRuns { get; init; } = [];
}

public sealed class WorkflowRun
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("display_title")]
    public string DisplayTitle { get; init; } = string.Empty;

    [JsonPropertyName("head_branch")]
    public string HeadBranch { get; init; } = string.Empty;

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; init; } = string.Empty;

    [JsonPropertyName("run_number")]
    public int RunNumber { get; init; }

    public string Event { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Conclusion { get; init; }

    [JsonPropertyName("workflow_id")]
    public long WorkflowId { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }
}
