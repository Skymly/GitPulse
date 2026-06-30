namespace GitPulse.Core.Models;

public sealed class Label
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
