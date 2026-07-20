namespace GitPulse.Core.Abstractions;

/// <summary>
/// Reports whether the main window is currently visible versus Tray Presence
/// (window hidden, process still running). Platform implementations live under
/// App/Platforms; Android may be a no-op that always reports visible.
/// </summary>
/// <remarks>
/// See ADR-010 and CONTEXT.md (<c>Tray Presence</c>).
/// </remarks>
public interface IAppPresence
{
    /// <summary>
    /// <see langword="true"/> when the main window is shown;
    /// <see langword="false"/> when hidden to the tray.
    /// </summary>
    bool IsMainWindowVisible { get; }
}
