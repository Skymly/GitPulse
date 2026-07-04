using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Pull request model. Extends the issue concept with PR-specific fields.
/// </summary>
public sealed class PullRequest
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public bool Draft { get; init; }
    public bool Merged { get; init; }
    public string HtmlUrl { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public User? User { get; init; }
    public User? MergedBy { get; init; }
    public string HeadRef { get; init; } = string.Empty;
    public string BaseRef { get; init; } = string.Empty;

    // ── M6: PR review & merge fields ─────────────────────────────

    /// <summary>Whether the PR can be merged (null = still computing).</summary>
    [JsonPropertyName("mergeable")]
    public bool? Mergeable { get; init; }

    /// <summary>"clean", "dirty", "unstable", "blocked", or null.</summary>
    [JsonPropertyName("mergeable_state")]
    public string? MergeableState { get; init; }

    /// <summary>SHA of the merge commit (set after merge).</summary>
    [JsonPropertyName("merge_commit_sha")]
    public string? MergeCommitSha { get; init; }

    /// <summary>Number of commits in the PR.</summary>
    [JsonPropertyName("commits")]
    public int Commits { get; init; }

    /// <summary>Lines added across all commits.</summary>
    [JsonPropertyName("additions")]
    public int Additions { get; init; }

    /// <summary>Lines deleted across all commits.</summary>
    [JsonPropertyName("deletions")]
    public int Deletions { get; init; }

    /// <summary>Number of files changed.</summary>
    [JsonPropertyName("changed_files")]
    public int ChangedFiles { get; init; }
}

/// <summary>
/// Merge method for <c>PUT /repos/{owner}/{repo}/pulls/{number}/merge</c>.
/// </summary>
public enum MergeMethod
{
    /// <summary>Create a merge commit (default).</summary>
    Merge,

    /// <summary>Squash all commits into one.</summary>
    Squash,

    /// <summary>Rebase commits onto the base branch.</summary>
    Rebase,
}

/// <summary>
/// Request body for <c>PUT /repos/{owner}/{repo}/pulls/{number}/merge</c>.
/// </summary>
public sealed class MergeRequest
{
    /// <summary>Optional commit message for the merge.</summary>
    [JsonPropertyName("commit_message")]
    public string? CommitMessage { get; set; }

    /// <summary>Optional commit title for the merge.</summary>
    [JsonPropertyName("commit_title")]
    public string? CommitTitle { get; set; }

    /// <summary>Merge method: "merge", "squash", or "rebase".</summary>
    [JsonPropertyName("merge_method")]
    public string Method { get; set; } = "merge";

    /// <summary>SHA that the PR head must match (optional safety check).</summary>
    [JsonPropertyName("sha")]
    public string? Sha { get; set; }
}

/// <summary>
/// Response from <c>PUT /repos/{owner}/{repo}/pulls/{number}/merge</c>.
/// </summary>
public sealed class MergeResponse
{
    [JsonPropertyName("sha")]
    public string Sha { get; init; } = string.Empty;

    [JsonPropertyName("merged")]
    public bool Merged { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
