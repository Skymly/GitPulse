using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

// ── Request bodies for GitHub REST API write operations ──────────────

/// <summary>
/// Request body for POST /repos/{owner}/{repo}/issues/{number}/comments
/// and POST /repos/{owner}/{repo}/pulls/{number}/comments.
/// </summary>
public sealed class CommentCreateRequest
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Request body for PATCH /repos/{owner}/{repo}/issues/{number}.
/// Only the fields being updated need to be set; GitHub treats null
/// fields as "unchanged".
/// </summary>
public sealed class IssueUpdateRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>"open" or "closed".</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>Array of label names to replace the current labels.</summary>
    [JsonPropertyName("labels")]
    public string[]? Labels { get; set; }
}

/// <summary>
/// Request body for POST /repos/{owner}/{repo}/issues.
/// </summary>
public sealed class IssueCreateRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>Optional array of label names to assign on creation.</summary>
    [JsonPropertyName("labels")]
    public string[]? Labels { get; set; }
}

/// <summary>
/// Request body for PUT /repos/{owner}/{repo}/issues/{number}/labels.
/// Replaces all labels on the issue with the given set.
/// </summary>
public sealed class LabelsReplaceRequest
{
    [JsonPropertyName("labels")]
    public string[] Labels { get; set; } = [];
}
