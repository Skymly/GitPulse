using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Review comment from <c>GET /repos/{owner}/{repo}/pulls/{pull_number}/comments</c>.
/// These are comments on specific lines of the diff, distinct from
/// conversation comments (issue comments).
/// </summary>
public sealed class ReviewComment
{
    public long Id { get; init; }

    [JsonPropertyName("pull_request_review_id")]
    public long? PullRequestReviewId { get; init; }

    /// <summary>The diff hunk text this comment belongs to.</summary>
    [JsonPropertyName("diff_hunk")]
    public string DiffHunk { get; init; } = string.Empty;

    /// <summary>File path the comment is on.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Position in the diff (deprecated, may be null).</summary>
    public int? Position { get; init; }

    [JsonPropertyName("original_position")]
    public int? OriginalPosition { get; init; }

    /// <summary>SHA of the commit the comment was left on.</summary>
    [JsonPropertyName("commit_id")]
    public string CommitId { get; init; } = string.Empty;

    [JsonPropertyName("original_commit_id")]
    public string OriginalCommitId { get; init; } = string.Empty;

    /// <summary>ID of the comment this is a reply to (null for top-level).</summary>
    [JsonPropertyName("in_reply_to_id")]
    public long? InReplyToId { get; init; }

    public User? User { get; init; }

    public string Body { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    /// <summary>Line number in the diff the comment applies to.</summary>
    public int Line { get; init; }

    [JsonPropertyName("original_line")]
    public int? OriginalLine { get; init; }

    /// <summary>"LEFT" or "RIGHT" — which side of the diff.</summary>
    public string Side { get; init; } = "RIGHT";

    [JsonPropertyName("start_line")]
    public int? StartLine { get; init; }

    [JsonPropertyName("start_side")]
    public string? StartSide { get; init; }

    /// <summary>"line" or "file".</summary>
    [JsonPropertyName("subject_type")]
    public string? SubjectType { get; init; }
}
