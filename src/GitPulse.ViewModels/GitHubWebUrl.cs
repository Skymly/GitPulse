using GitPulse.Core.Models;

namespace GitPulse.ViewModels;

/// <summary>
/// Maps GitHub REST API URLs and repo coordinates onto github.com pages.
/// </summary>
public static class GitHubWebUrl
{
    public static string RepoHome(string owner, string repo)
        => $"https://github.com/{EncodeSegment(owner)}/{EncodeSegment(repo)}";

    public static string RepoBlob(string owner, string repo, string path, string gitRef = "HEAD")
        => $"{RepoHome(owner, repo)}/blob/{EncodeSegment(gitRef)}/{EncodePath(path)}";

    public static string RepoTree(string owner, string repo, string path, string gitRef = "HEAD")
    {
        if (string.IsNullOrEmpty(path))
            return RepoHome(owner, repo);

        return $"{RepoHome(owner, repo)}/tree/{EncodeSegment(gitRef)}/{EncodePath(path)}";
    }

    public static string? FromNotification(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return FromApiUrl(notification.Subject.Url)
            ?? NullIfEmpty(notification.Repository.HtmlUrl);
    }

    public static string? FromApiUrl(string? apiUrl)
    {
        if (string.IsNullOrEmpty(apiUrl))
            return null;

        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
            return null;

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return apiUrl;

        if (!uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3
            || !segments[0].Equals("repos", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var owner = segments[1];
        var repo = segments[2];
        if (segments.Length < 5)
            return $"https://github.com/{owner}/{repo}";

        var resource = segments[3];
        var id = segments[4];
        if (resource.Equals("issues", StringComparison.OrdinalIgnoreCase)
            && !id.Equals("comments", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://github.com/{owner}/{repo}/issues/{id}";
        }

        if (resource.Equals("pulls", StringComparison.OrdinalIgnoreCase))
            return $"https://github.com/{owner}/{repo}/pull/{id}";

        if (resource.Equals("commits", StringComparison.OrdinalIgnoreCase))
            return $"https://github.com/{owner}/{repo}/commit/{id}";

        if (resource.Equals("releases", StringComparison.OrdinalIgnoreCase))
            return $"https://github.com/{owner}/{repo}/releases/{id}";

        return $"https://github.com/{owner}/{repo}";
    }

    private static string EncodePath(string path)
        => string.Join('/', path.Split('/', StringSplitOptions.None).Select(EncodeSegment));

    private static string EncodeSegment(string value) => Uri.EscapeDataString(value);

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
