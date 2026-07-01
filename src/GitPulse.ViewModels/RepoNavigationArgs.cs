namespace GitPulse.ViewModels;

/// <summary>
/// Navigation parameter for the Issues page, carrying the repository
/// owner and name to query.
/// </summary>
public sealed record RepoNavigationArgs(string Owner, string Repo);
