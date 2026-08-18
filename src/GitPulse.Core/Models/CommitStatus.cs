using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Combined commit-status payload from
/// <c>GET /repos/{owner}/{repo}/commits/{ref}/status</c>.
/// Aggregates Commit Statuses only — not Check Runs.
/// </summary>
public sealed class CombinedCommitStatus
{
    public string State { get; init; } = string.Empty;

    public string Sha { get; init; } = string.Empty;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    public CommitStatus[] Statuses { get; init; } = [];
}

/// <summary>
/// A classic Commit Status on a commit (<c>context</c> + <c>state</c>).
/// Distinct from a Check Run.
/// </summary>
public sealed class CommitStatus
{
    public long Id { get; init; }

    public string State { get; init; } = string.Empty;

    public string? Description { get; init; }

    [JsonPropertyName("target_url")]
    public string? TargetUrl { get; init; }

    public string Context { get; init; } = string.Empty;
}
