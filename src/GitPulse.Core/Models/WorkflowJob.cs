using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Wrapper for <c>GET /repos/{owner}/{repo}/actions/runs/{run_id}/jobs</c>.
/// </summary>
public sealed class WorkflowJobsResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("jobs")]
    public WorkflowJob[] Jobs { get; init; } = [];
}

public sealed class WorkflowJob
{
    public long Id { get; init; }

    [JsonPropertyName("run_id")]
    public long RunId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Conclusion { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; init; }
}
