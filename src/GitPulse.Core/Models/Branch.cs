using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Branch from <c>GET /repos/{owner}/{repo}/branches</c>.
/// </summary>
public sealed class Branch
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Latest commit on this branch.</summary>
    public BranchCommit Commit { get; init; } = new();

    /// <summary>C# keyword; needs explicit JSON mapping.</summary>
    [JsonPropertyName("protected")]
    public bool Protected { get; init; }
}

/// <summary>Minimal commit reference embedded in a <see cref="Branch"/>.</summary>
public sealed class BranchCommit
{
    public string Sha { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}
