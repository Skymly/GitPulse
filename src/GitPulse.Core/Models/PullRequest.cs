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
}
