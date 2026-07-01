using GitPulse.Core.Abstractions;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// <see cref="IBrowserLauncher"/> stub that records opened URLs instead of
/// launching a browser. Used by IssueDetailViewModel / PullRequestDetailViewModel
/// tests to assert the OpenInBrowser command.
/// </summary>
public sealed class FakeBrowserLauncher : IBrowserLauncher
{
    public List<string> OpenedUrls { get; } = [];

    public Task OpenAsync(string url)
    {
        OpenedUrls.Add(url);
        return Task.CompletedTask;
    }
}
