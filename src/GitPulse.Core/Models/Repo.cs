using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

/// <summary>
/// Minimal repository model. Fields will be expanded as features land.
/// </summary>
public sealed class Repo
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    public string? Description { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("clone_url")]
    public string CloneUrl { get; init; } = string.Empty;

    [JsonPropertyName("ssh_url")]
    public string SshUrl { get; init; } = string.Empty;

    public bool Private { get; init; }

    [JsonPropertyName("default_branch")]
    public string? DefaultBranch { get; init; }

    [JsonPropertyName("stargazers_count")]
    public int StargazersCount { get; init; }

    [JsonPropertyName("forks_count")]
    public int ForksCount { get; init; }

    [JsonPropertyName("open_issues_count")]
    public int OpenIssuesCount { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    public string? Language { get; init; }

    public RepoLicense? License { get; init; }

    public string[] Topics { get; init; } = [];

    public string? Homepage { get; init; }
}

/// <summary>License summary from GET repository.</summary>
public sealed class RepoLicense
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("spdx_id")]
    public string? SpdxId { get; init; }
}
