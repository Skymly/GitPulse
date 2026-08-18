using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Wrapper for <c>GET /repos/{owner}/{repo}/commits/{ref}/check-runs</c>.
/// </summary>
public sealed class CheckRunsResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("check_runs")]
    public CheckRun[] CheckRuns { get; init; } = [];
}

/// <summary>
/// A Check Run on a commit (GitHub Checks API). Distinct from a Commit Status
/// and from an Actions Workflow Run.
/// </summary>
public sealed class CheckRun
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Conclusion { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("details_url")]
    public string? DetailsUrl { get; init; }

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; init; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; init; }
}
