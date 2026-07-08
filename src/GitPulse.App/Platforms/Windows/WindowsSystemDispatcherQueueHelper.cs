using Microsoft.UI.Dispatching;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Ensures a <see cref="DispatcherQueueController"/> exists on the current
/// thread. Required by <see cref="MicaController"/> and
/// <see cref="DesktopAcrylicController"/> to manage composition callbacks.
/// Ported from the official WinUI Gallery SystemBackdrops sample.
/// </summary>
internal sealed class WindowsSystemDispatcherQueueHelper
{
    private DispatcherQueueController? _dispatcherQueueController;

    public void EnsureWindowsSystemDispatcherQueueController()
    {
        if (DispatcherQueue.GetForCurrentThread() is not null)
            return;

        _dispatcherQueueController = DispatcherQueueController.CreateOnDedicatedThread();
    }
}
