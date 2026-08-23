using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Pending review requests on a pull request from
/// <c>GET /repos/{owner}/{repo}/pulls/{number}/requested_reviewers</c>.
/// Distinct from a submitted <see cref="PullRequestReview"/>.
/// </summary>
public sealed class RequestedReviewers
{
    public User[] Users { get; init; } = [];

    public Team[] Teams { get; init; } = [];
}

/// <summary>A GitHub team listed on a review request (slug/name only).</summary>
public sealed class Team
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;
}

/// <summary>
/// Body for add/remove requested reviewers.
/// <see cref="Reviewers"/> are user logins; <see cref="TeamReviewers"/> are team slugs.
/// </summary>
public sealed class ReviewersRequest
{
    public string[] Reviewers { get; set; } = [];

    [JsonPropertyName("team_reviewers")]
    public string[]? TeamReviewers { get; set; }
}
