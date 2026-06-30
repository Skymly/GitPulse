namespace GitPulse.Core.Models;

/// <summary>
/// Minimal repository model. Fields will be expanded as features land.
/// </summary>
public sealed class Repo
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string HtmlUrl { get; init; } = string.Empty;
    public bool Private { get; init; }
    public string? DefaultBranch { get; init; }
    public int StargazersCount { get; init; }
    public int ForksCount { get; init; }
    public int OpenIssuesCount { get; init; }
    public DateTime UpdatedAt { get; init; }
}
