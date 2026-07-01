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
        if (!string.IsNullOrEmpty(url))
            await Launcher.OpenAsync(url);
    }
}
