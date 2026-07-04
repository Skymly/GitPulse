using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// <see cref="INotificationPoller"/> stub for ViewModel tests. Allows
/// manual control of polling state and simulated notification updates.
/// </summary>
public sealed class FakeNotificationPoller : INotificationPoller
{
    public event Action<Notification[], int>? NotificationsUpdated;
    public event Action<bool>? IsPollingChanged;

    public int UnreadCount { get; private set; }
    public bool IsPolling { get; private set; }
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);

    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public int RefreshCallCount { get; private set; }

    public void Start()
    {
        StartCallCount++;
        IsPolling = true;
        IsPollingChanged?.Invoke(true);
    }

    public void Stop()
    {
        StopCallCount++;
        IsPolling = false;
        IsPollingChanged?.Invoke(false);
    }

    public Task RefreshAsync()
    {
        RefreshCallCount++;
        return Task.CompletedTask;
    }

    /// <summary>Simulate a poll cycle firing with the given notifications.</summary>
    public void SimulateNotifications(Notification[] notifications, int unreadCount)
    {
        UnreadCount = unreadCount;
        NotificationsUpdated?.Invoke(notifications, unreadCount);
    }

    public void Dispose()
    {
    }
}
