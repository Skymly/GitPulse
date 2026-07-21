using GitPulse.Core.Abstractions;

namespace GitPulse.App.Platforms.Android;

/// <summary>
/// Android no-op <see cref="IAppPresence"/>: always reports the main window
/// as visible (no tray). See ADR-010.
/// </summary>
public sealed class AndroidAppPresence : IAppPresence
{
    /// <inheritdoc />
    public bool IsMainWindowVisible => true;
}
