using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Diff entry from <c>GET /repos/{owner}/{repo}/pulls/{pull_number}/files</c>.
/// Each entry describes a file changed in the pull request, including the
/// unified diff <see cref="Patch"/> text.
/// </summary>
public sealed class DiffEntry
{
    public string? Sha { get; init; }

    /// <summary>Relative path of the file in the repo.</summary>
    public string Filename { get; init; } = string.Empty;

    /// <summary>"added", "removed", "modified", "renamed", "copied", "changed", "unchanged".</summary>
    public string Status { get; init; } = string.Empty;

    public int Additions { get; init; }
    public int Deletions { get; init; }
    public int Changes { get; init; }

    [JsonPropertyName("blob_url")]
    public string BlobUrl { get; init; } = string.Empty;

    [JsonPropertyName("raw_url")]
    public string RawUrl { get; init; } = string.Empty;

    [JsonPropertyName("contents_url")]
    public string ContentsUrl { get; init; } = string.Empty;

    /// <summary>
    /// Unified diff patch text. Null for binary files.
    /// </summary>
    public string? Patch { get; init; }

    /// <summary>Previous filename (only set for renamed files).</summary>
    [JsonPropertyName("previous_filename")]
    public string? PreviousFilename { get; init; }
}
