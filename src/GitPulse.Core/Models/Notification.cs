using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// GitHub notification thread. Maps to the GitHub REST API
/// <c>GET /notifications</c> response.
/// </summary>
public sealed class Notification
{
    /// <summary>The notification thread ID (used for mark-as-read).</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Whether the notification has been read.</summary>
    [JsonPropertyName("unread")]
    public bool Unread { get; init; }

    /// <summary>Reason the notification was triggered: "assign", "author", "comment", "mention", "review_requested", etc.</summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    /// <summary>When the notification was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    /// <summary>The URL of the notification thread (GitHub API).</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>The HTML URL of the subject (issue/PR/commit web page).</summary>
    [JsonPropertyName("subject")]
    public NotificationSubject Subject { get; init; } = new();

    /// <summary>The repository the notification belongs to.</summary>
    [JsonPropertyName("repository")]
    public NotificationRepository Repository { get; init; } = new();
}

/// <summary>The subject (issue, PR, commit, etc.) that triggered the notification.</summary>
public sealed class NotificationSubject
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>"Issue", "PullRequest", "Commit", "Release", "Discussion", "CheckSuite", etc.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>Web URL of the subject (may be null for some types).</summary>
    [JsonPropertyName("latest_comment_url")]
    public string? LatestCommentUrl { get; init; }
}

/// <summary>Minimal repository info embedded in a notification.</summary>
public sealed class NotificationRepository
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;
}
