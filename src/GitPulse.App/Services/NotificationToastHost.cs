using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.Core.Notifications;

namespace GitPulse.App.Services;

/// <summary>
/// App-layer wiring for ADR-010: keeps the Notification Poller running for the
/// process lifetime, forwards poll results to
/// <see cref="NotificationToastCoordinator"/>, and resets the toast baseline
/// when entering Tray Presence.
/// </summary>
public sealed class NotificationToastHost : IDisposable
{
    private readonly INotificationPoller _poller;
    private readonly NotificationToastCoordinator _coordinator;
    private bool _disposed;

    public NotificationToastHost(
        INotificationPoller poller,
        NotificationToastCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(poller);
        ArgumentNullException.ThrowIfNull(coordinator);

        _poller = poller;
        _coordinator = coordinator;
        _poller.NotificationsUpdated += OnNotificationsUpdated;
        _poller.Start();
    }

    /// <summary>
    /// Treat the next poll as a baseline (no Toast). Call when the main window
    /// enters Tray Presence so existing unread items do not flood the user.
    /// </summary>
    public void OnEnteredTrayPresence()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _coordinator.ResetBaseline();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _poller.NotificationsUpdated -= OnNotificationsUpdated;
        _poller.Stop();
    }

    private void OnNotificationsUpdated(Notification[] notifications, int _)
    {
        _coordinator.HandleNotificationsUpdated(notifications);
    }
}
