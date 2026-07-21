using System.Runtime.InteropServices;
using GitPulse.Core.Abstractions;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Windows <see cref="IToastNotifier"/> using Windows App SDK
/// <see cref="AppNotificationManager"/> (unpackaged-friendly). Activation
/// raises <see cref="Activated"/>. See ADR-010.
/// </summary>
public sealed class WindowsToastNotifier : IToastNotifier, IDisposable
{
    private const string AppUserModelId = "Skymly.GitPulse";

    private readonly object _gate = new();
    private bool _disposed;
    private bool _initialized;
    private bool _handlerAttached;

    /// <summary>
    /// Fired when the user activates a summary Toast (click / tap).
    /// </summary>
    public event Action? Activated;

    /// <summary>
    /// Registers the notification manager after the WinUI window is ready.
    /// Safe to call multiple times.
    /// </summary>
    public void EnsureInitialized()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_initialized)
                return;

            try
            {
                _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

                var manager = AppNotificationManager.Default;
                manager.NotificationInvoked += OnNotificationInvoked;
                _handlerAttached = true;
                manager.Register();
                _initialized = true;
            }
            catch (Exception ex)
            {
                CrashLog.Write("Toast EnsureInitialized failed", ex);
                if (_handlerAttached)
                {
                    try
                    {
                        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                    }
                    catch
                    {
                        // ignore
                    }

                    _handlerAttached = false;
                }
            }
        }
    }

    /// <inheritdoc />
    public void ShowNewNotificationsSummary(int newCount)
    {
        if (newCount <= 0)
            return;

        EnsureInitialized();

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_initialized)
                return;
        }

        var message = newCount == 1
            ? "1 new notification"
            : $"{newCount} new notifications";

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText("GitPulse")
                .AddText(message)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Toast Show failed", ex);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_handlerAttached)
            {
                try
                {
                    AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                    if (_initialized)
                        AppNotificationManager.Default.Unregister();
                }
                catch (Exception ex)
                {
                    CrashLog.Write("Toast Dispose failed", ex);
                }

                _handlerAttached = false;
                _initialized = false;
            }
        }
    }

    private void OnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
        _ = sender;
        _ = args;
        try
        {
            Activated?.Invoke();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Toast Activated handler failed", ex);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}
