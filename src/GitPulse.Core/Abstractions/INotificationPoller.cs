using GitPulse.Core.Models;

namespace GitPulse.Core.Abstractions;

/// <summary>
/// Polls GitHub notifications on a timer and exposes the results via events.
/// This is the M4 Events domain showcase: a polling timer drives periodic
/// HTTP fetches, and notification updates flow to the UI via reactive
/// pipelines in the ViewModel.
/// </summary>
/// <remarks>
/// <para>
/// The poller is a singleton service — one instance shared across the app.
/// It starts polling when the app is in the foreground and stops when
/// backgrounded, conserving API rate limit and battery.
/// </para>
/// <para>
/// <b>Events domain showcase:</b> The polling timer is an R3
/// <c>Observable.Interval</c> stream (in the Services implementation).
/// Each tick triggers an HTTP fetch via
/// <c>IGitHubReposApi.ListNotifications</c>, and the results are published
/// via <see cref="NotificationsUpdated"/>. The ViewModel bridges this event
/// to R3 <c>BindableReactiveProperty</c> for UI binding — demonstrating the
/// reactive pipeline pattern: timer → HTTP → deserialize → event → R3 → UI.
/// </para>
/// <para>
/// The interface uses plain .NET events (not R3 types) so it can live in
/// Core without a reactive framework dependency. The Services implementation
/// wraps R3 internally.
/// </para>
/// </remarks>
public interface INotificationPoller : IDisposable
{
    /// <summary>
    /// Fired on each poll cycle with the latest notifications and unread count.
    /// Also fires on manual refresh. Fires with an empty array when
    /// unauthenticated.
    /// </summary>
    event Action<Notification[], int>? NotificationsUpdated;

    /// <summary>Current unread notification count.</summary>
    int UnreadCount { get; }

    /// <summary>Whether the poller is actively polling (foreground).</summary>
    bool IsPolling { get; }

    /// <summary>Fired when <see cref="IsPolling"/> changes.</summary>
    event Action<bool>? IsPollingChanged;

    /// <summary>Polling interval (default 60 seconds).</summary>
    TimeSpan PollInterval { get; set; }

    /// <summary>Start polling. Called when the app enters the foreground.</summary>
    void Start();

    /// <summary>Stop polling. Called when the app enters the background.</summary>
    void Stop();

    /// <summary>Trigger an immediate poll (manual refresh).</summary>
    Task RefreshAsync();
}
