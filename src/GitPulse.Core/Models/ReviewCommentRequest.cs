using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Request body for <c>POST /repos/{owner}/{repo}/pulls/{pull_number}/comments</c>.
/// Creates a review comment on a specific line of the diff.
/// </summary>
public sealed class ReviewCommentRequest
{
    /// <summary>Comment text (Markdown supported).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>SHA of the commit to comment on.</summary>
    [JsonPropertyName("commit_id")]
    public string CommitId { get; set; } = string.Empty;

    /// <summary>Relative path to the file.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>"LEFT" (deletion) or "RIGHT" (addition/context).</summary>
    public string Side { get; set; } = "RIGHT";

    /// <summary>
    /// Line in the diff. Omit for file-level comments
    /// (<see cref="SubjectType"/> = <c>file</c>).
    /// </summary>
    [JsonPropertyName("line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; set; }

    /// <summary>
    /// <c>line</c> (default) or <c>file</c>. File comments must omit
    /// <see cref="Line"/>.
    /// </summary>
    [JsonPropertyName("subject_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubjectType { get; set; }

    /// <summary>
    /// ID of the review comment to reply to. When set, all other
    /// parameters except <see cref="Body"/> are ignored.
    /// </summary>
    [JsonPropertyName("in_reply_to")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? InReplyTo { get; set; }
}
