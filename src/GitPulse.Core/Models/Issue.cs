using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

public sealed class Issue
{
    public long Id { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public User? User { get; init; }
    public bool IsPullRequest { get; init; }

    /// <summary>GitHub returns a non-null object when the issue is actually a PR.</summary>
    [JsonPropertyName("pull_request")]
    public PullRequestRef? PullRequestRef { get; init; }

    public int CommentsCount { get; init; }
    public Label[] Labels { get; init; } = [];
    public string? MilestoneTitle { get; init; }
}

/// <summary>Minimal embedded PR reference inside an Issue payload.</summary>
public sealed class PullRequestRef
{
    public string Url { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public bool? Merged { get; init; }
    public string? DiffUrl { get; init; }
}
