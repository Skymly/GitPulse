namespace GitPulse.Core.Models;

public sealed class Issue
{
    public long Id { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public User? User { get; init; }
    public bool IsPullRequest { get; init; }
}
