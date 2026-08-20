using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// A repository commit from <c>GET /repos/{owner}/{repo}/commits</c>
/// and <c>GET /repos/{owner}/{repo}/commits/{ref}</c>.
/// Distinct from a Check Run and from a file-edit FileCommitResponse.
/// List payloads omit <see cref="Stats"/> and <see cref="Files"/>; Get-a-commit fills them.
/// </summary>
public sealed class GitCommit
{
    public string Sha { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    public GitCommitDetail? Commit { get; init; }

    public User? Author { get; init; }

    /// <summary>
    /// Addition/deletion totals from Get-a-commit. Null on list-commits payloads.
    /// </summary>
    public GitCommitStats? Stats { get; init; }

    /// <summary>
    /// Changed files from Get-a-commit, using the existing diff-entry shape.
    /// Empty on list-commits payloads. <see cref="DiffEntry.Patch"/> may be null.
    /// </summary>
    public DiffEntry[] Files { get; init; } = [];
}

/// <summary>Nested Git commit payload (message + author identity).</summary>
public sealed class GitCommitDetail
{
    public string Message { get; init; } = string.Empty;

    public GitCommitAuthor? Author { get; init; }
}

/// <summary>Git-level author on a commit (name/date, not a GitHub User).</summary>
public sealed class GitCommitAuthor
{
    public string Name { get; init; } = string.Empty;

    public DateTime Date { get; init; }
}

/// <summary>
/// Line-count totals on a Git Commit from Get-a-commit
/// (<c>additions</c>, <c>deletions</c>, <c>total</c>).
/// </summary>
public sealed class GitCommitStats
{
    public int Additions { get; init; }

    public int Deletions { get; init; }

    public int Total { get; init; }
}
