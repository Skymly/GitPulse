using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

public sealed class CodeSearchItem
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Sha { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    public Repo Repository { get; init; } = new();
}
