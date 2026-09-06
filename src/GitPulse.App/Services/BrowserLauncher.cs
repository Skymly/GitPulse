using GitPulse.Core.Abstractions;

namespace GitPulse.App.Services;

/// <summary>
/// MAUI-backed <see cref="IBrowserLauncher"/> using <c>Launcher.OpenAsync</c>.
/// Kept in the App layer so ViewModels stay MAUI-free and testable.
/// Only <c>http</c>/<c>https</c> URLs are launched so API JSON links,
/// <c>javascript:</c>, and <c>file:</c> values from GitHub payloads are ignored.
/// </summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    public async Task OpenAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        if (uri.Scheme is not ("http" or "https"))
            return;

        await Launcher.OpenAsync(uri);
    }
}
