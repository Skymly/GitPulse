namespace GitPulse.Core.Models;

public sealed class User
{
    public long Id { get; init; }
    public string Login { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
}
