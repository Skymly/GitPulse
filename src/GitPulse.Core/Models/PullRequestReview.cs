using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Submitted Pull Request Review from
/// <c>GET/POST /repos/{owner}/{repo}/pulls/{number}/reviews</c>.
/// <see cref="State"/> is GitHub’s submitted state (e.g. APPROVED), not the
/// create-time <see cref="PullRequestReviewCreateRequest.Event"/>.
/// </summary>
public sealed class PullRequestReview
{
    public long Id { get; init; }

    public User? User { get; init; }

    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// GitHub submitted state: APPROVED, CHANGES_REQUESTED, COMMENTED,
    /// DISMISSED, or PENDING.
    /// </summary>
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("submitted_at")]
    public DateTime? SubmittedAt { get; init; }

    [JsonPropertyName("commit_id")]
    public string? CommitId { get; init; }
}

/// <summary>
/// Request body for <c>POST /repos/{owner}/{repo}/pulls/{number}/reviews</c>.
/// Immediate submit only: <see cref="Event"/> is required (no pending review).
/// </summary>
public sealed class PullRequestReviewCreateRequest
{
    /// <summary>Optional SHA; GitHub defaults to the latest head commit.</summary>
    [JsonPropertyName("commit_id")]
    public string? CommitId { get; set; }

    /// <summary>Required for REQUEST_CHANGES and COMMENT; optional for APPROVE.</summary>
    public string? Body { get; set; }

    /// <summary>Review Event: APPROVE, REQUEST_CHANGES, or COMMENT.</summary>
    public string Event { get; set; } = "COMMENT";
}
