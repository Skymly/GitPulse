using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

public sealed class Comment
{
    public long Id { get; init; }
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    public User? User { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;
}
