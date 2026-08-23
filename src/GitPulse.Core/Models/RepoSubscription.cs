using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// A repository watching subscription from
/// <c>GET/PUT /repos/{owner}/{repo}/subscription</c>.
/// Distinct from starring and from a notification thread subscription.
/// </summary>
public sealed class RepoSubscription
{
    public bool Subscribed { get; init; }

    public bool Ignored { get; init; }

    public string? Reason { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }
}

/// <summary>
/// Body for <c>PUT /repos/{owner}/{repo}/subscription</c>.
/// Watch uses subscribed=true, ignored=false.
/// </summary>
public sealed class RepoSubscriptionRequest
{
    public bool Subscribed { get; set; }

    public bool Ignored { get; set; }
}
