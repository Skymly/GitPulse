using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

public sealed class SearchIssueItem
{
    public long Id { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("repository_url")]
    public string RepositoryUrl { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    public User? User { get; init; }

    [JsonPropertyName("pull_request")]
    public PullRequestRef? PullRequest { get; init; }
}
