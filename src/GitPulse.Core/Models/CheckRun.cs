using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Wrapper for <c>GET /repos/{owner}/{repo}/commits/{ref}/check-runs</c>.
/// </summary>
public sealed class CheckRunsResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("check_runs")]
    public CheckRun[] CheckRuns { get; init; } = [];
}

/// <summary>
/// A Check Run on a commit (GitHub Checks API). Distinct from a Commit Status
/// and from an Actions Workflow Run.
/// </summary>
public sealed class CheckRun
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Conclusion { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("details_url")]
    public string? DetailsUrl { get; init; }

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; init; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; init; }

    /// <summary>Optional output from Get-a-check-run (title / summary / text).</summary>
    public CheckRunOutput? Output { get; init; }
}

/// <summary>Check Run output block. Distinct from Check Run Annotation (M27).</summary>
public sealed class CheckRunOutput
{
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Text { get; init; }

    [JsonPropertyName("annotations_count")]
    public int AnnotationsCount { get; init; }
}

/// <summary>
/// A Check Run annotation from
/// <c>GET /repos/{owner}/{repo}/check-runs/{check_run_id}/annotations</c>.
/// First page only in M27.
/// </summary>
public sealed class CheckRunAnnotation
{
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("start_line")]
    public int StartLine { get; init; }

    [JsonPropertyName("end_line")]
    public int EndLine { get; init; }

    [JsonPropertyName("annotation_level")]
    public string AnnotationLevel { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Title { get; init; }
}

