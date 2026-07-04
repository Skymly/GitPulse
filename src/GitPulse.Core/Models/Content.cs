using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Directory entry from <c>GET /repos/{owner}/{repo}/contents/{path}</c>
/// when the path is a directory. Each entry describes a file or subdirectory.
/// </summary>
public sealed class ContentEntry
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>"file" or "dir".</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; init; } = string.Empty;

    /// <summary>File size in bytes (0 for directories).</summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    /// <summary>Raw download URL (files only, null for directories).</summary>
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; init; }
}

/// <summary>
/// File content from <c>GET /repos/{owner}/{repo}/contents/{path}</c>
/// when the path is a file. The <see cref="Content"/> field is base64-encoded.
/// </summary>
public sealed class FileContent
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; init; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>Base64-encoded file content.</summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>Encoding type — typically "base64".</summary>
    [JsonPropertyName("encoding")]
    public string Encoding { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; init; }
}

/// <summary>
/// Request body for <c>PUT /repos/{owner}/{repo}/contents/{path}</c>
/// (create or update file).
/// </summary>
public sealed class FileUpdateRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Base64-encoded file content.</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// SHA of the existing file (required for updates, omit for creates).
    /// </summary>
    [JsonPropertyName("sha")]
    public string? Sha { get; set; }

    /// <summary>Target branch (optional, defaults to repo default branch).</summary>
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
}

/// <summary>
/// Request body for <c>DELETE /repos/{owner}/{repo}/contents/{path}</c>.
/// </summary>
public sealed class FileDeleteRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>SHA of the file being deleted (required).</summary>
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
}

/// <summary>
/// Response from create/update/delete file operations.
/// Contains the updated content and commit info.
/// </summary>
public sealed class FileCommitResponse
{
    [JsonPropertyName("content")]
    public FileContent? Content { get; init; }

    [JsonPropertyName("commit")]
    public FileCommit? Commit { get; init; }
}

/// <summary>Commit metadata returned by file operations.</summary>
public sealed class FileCommit
{
    [JsonPropertyName("sha")]
    public string Sha { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
