using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// A repository commit from <c>GET /repos/{owner}/{repo}/commits</c>.
/// Distinct from a Check Run and from a file-edit FileCommitResponse.
/// </summary>
public sealed class GitCommit
{
    public string Sha { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    public GitCommitDetail? Commit { get; init; }

    public User? Author { get; init; }
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
