using CommunityToolkit.Mvvm.Messaging;

namespace GitPulse.App.Events;

/// <summary>
/// Which repo list should reload after a successful write.
/// Shell <c>OnAppearing</c> skips when the query is unchanged, so writers
/// publish this instead of a <c>refresh=</c> query (pop does not retarget the page below).
/// </summary>
internal enum RepoListKind
{
    Issues,
    PullRequests,
    Files,
}

/// <summary>
/// Marks a repo's list stale so the living list page reloads on next appear.
/// </summary>
internal sealed class RepoListStaleMessage(RepoListKind kind, string owner, string repo)
{
    public RepoListKind Kind { get; } = kind;

    public string Owner { get; } = owner;

    public string Repo { get; } = repo;
}

internal static class RepoListStale
{
    public static void Send(RepoListKind kind, string owner, string repo) =>
        WeakReferenceMessenger.Default.Send(new RepoListStaleMessage(kind, owner, repo));

    public static bool Matches(
        RepoListStaleMessage message,
        RepoListKind kind,
        string ownerQuery,
        string repoQuery)
    {
        if (message.Kind != kind)
            return false;

        var owner = Uri.UnescapeDataString(ownerQuery);
        var repo = Uri.UnescapeDataString(repoQuery);
        return string.Equals(message.Owner, owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(message.Repo, repo, StringComparison.OrdinalIgnoreCase);
    }
}
