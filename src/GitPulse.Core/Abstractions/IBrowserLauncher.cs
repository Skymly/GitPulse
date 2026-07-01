namespace GitPulse.Core.Abstractions;

/// <summary>
/// Opens a URL in the platform default browser. Abstracts MAUI
/// <c>Launcher.OpenAsync</c> so ViewModels can be tested without a MAUI host.
/// </summary>
public interface IBrowserLauncher
{
    Task OpenAsync(string url);
}
