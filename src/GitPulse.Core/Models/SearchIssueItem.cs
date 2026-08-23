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

    /// <summary>
    /// owner/repo parsed from <see cref="RepositoryUrl"/>
    /// (https://api.github.com/repos/{owner}/{repo}). Empty when the
    /// URL is missing or not a repos path. Display-only — not a JSON field.
    /// </summary>
    [JsonIgnore]
    public string RepositoryFullName
    {
        get
        {
            if (!Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri))
                return string.Empty;

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3
                || !segments[0].Equals("repos", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var owner = Uri.UnescapeDataString(segments[1]);
            var repo = Uri.UnescapeDataString(segments[2]);
            return owner.Length == 0 || repo.Length == 0 ? string.Empty : owner + "/" + repo;
        }
    }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    public User? User { get; init; }

    [JsonPropertyName("pull_request")]
    public PullRequestRef? PullRequest { get; init; }
}
