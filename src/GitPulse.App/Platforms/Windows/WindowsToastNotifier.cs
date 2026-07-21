using CommunityToolkit.WinUI.Notifications;
using GitPulse.Core.Abstractions;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Windows <see cref="IToastNotifier"/> using OS Toast notifications
/// (CommunityToolkit). Activation raises <see cref="Activated"/>. See ADR-010.
/// </summary>
public sealed class WindowsToastNotifier : IToastNotifier, IDisposable
{
    private readonly object _gate = new();
    private bool _disposed;
    private bool _handlerAttached;

    /// <summary>
    /// Fired when the user activates a summary Toast (click / tap).
    /// </summary>
    public event Action? Activated;

    public WindowsToastNotifier()
    {
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        _handlerAttached = true;
    }

    /// <inheritdoc />
    public void ShowNewNotificationsSummary(int newCount)
    {
        if (newCount <= 0)
            return;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        var message = newCount == 1
            ? "1 new notification"
            : $"{newCount} new notifications";

        new ToastContentBuilder()
            .AddText("GitPulse")
            .AddText(message)
            .AddArgument("action", "notifications")
            .Show();
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
                ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
                _handlerAttached = false;
            }
        }
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat args)
    {
        _ = args;
        Activated?.Invoke();
    }
}
