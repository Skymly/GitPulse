using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

public sealed class User
{
    public long Id { get; init; }
    public string Login { get; init; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;
}
