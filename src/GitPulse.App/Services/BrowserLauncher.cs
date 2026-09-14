using GitPulse.Core.Abstractions;

namespace GitPulse.App.Services;

/// <summary>
/// MAUI-backed <see cref="IBrowserLauncher"/> using <c>Launcher.OpenAsync</c>.
/// Kept in the App layer so ViewModels stay MAUI-free and testable.
/// </summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    public async Task OpenAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;
        if (!IsAllowedScheme(uri.Scheme))
            return;

        await Launcher.OpenAsync(uri);
    }

    private static bool IsAllowedScheme(string scheme)
        => scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
