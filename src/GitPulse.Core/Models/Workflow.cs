using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Wrapper for GET /repos/{owner}/{repo}/actions/workflows.
/// </summary>
public sealed class WorkflowsResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    public Workflow[] Workflows { get; init; } = [];
}

/// <summary>A repository workflow from the Actions workflows list.</summary>
public sealed class Workflow
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;
}

/// <summary>Body for POST .../actions/workflows/{id}/dispatches.</summary>
public sealed class WorkflowDispatchRequest
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "main";
}
