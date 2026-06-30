namespace GitPulse.Core.Models;

public sealed class Comment
{
    public long Id { get; init; }
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public User? User { get; init; }
    public string HtmlUrl { get; init; } = string.Empty;
}
