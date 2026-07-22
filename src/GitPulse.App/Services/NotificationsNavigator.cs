namespace GitPulse.App.Services;

/// <summary>
/// Shows the main window (when applicable) and navigates to the Notifications
/// Shell tab. Used by tray menu and Toast activation.
/// </summary>
public sealed class NotificationsNavigator
{
    private readonly Action _showMainWindow;

    public NotificationsNavigator(Action? showMainWindow = null)
    {
        _showMainWindow = showMainWindow ?? (() => { });
    }

    public void OpenNotifications()
    {
        // Toast activation and tray menu may raise on a non-UI thread.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _showMainWindow();

            if (Shell.Current is null)
                return;

            try
            {
                await Shell.Current.GoToAsync("//NotificationsPage");
            }
            catch
            {
                // Shell may not be ready during early startup; ignore.
            }
        });
    }
}
