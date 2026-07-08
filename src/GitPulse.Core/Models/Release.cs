using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Release from <c>GET /repos/{owner}/{repo}/releases</c>.
/// The <see cref="Body"/> field contains release notes as Markdown.
/// </summary>
public sealed class Release
{
    public long Id { get; init; }

    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    public string? Name { get; init; }
    public string? Body { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    public bool Draft { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; init; }

    public User? Author { get; init; }
}
