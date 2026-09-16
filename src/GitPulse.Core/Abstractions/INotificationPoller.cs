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
/// App starts it at process start and stops it on Exit (host dispose).
/// Polling continues while the app is backgrounded or in Tray Presence
/// (ADR-010). There is no OnSleep / OnResume hook.
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

    /// <summary>Whether the poller is actively polling.</summary>
    bool IsPolling { get; }

    /// <summary>Fired when <see cref="IsPolling"/> changes.</summary>
    event Action<bool>? IsPollingChanged;

    /// <summary>
    /// Last poll failure; <c>null</c> after a successful poll or an
    /// unauthenticated stop.
    /// </summary>
    string? LastError { get; }

    /// <summary>Fired when <see cref="LastError"/> changes.</summary>
    event Action<string?>? LastErrorChanged;

    /// <summary>Polling interval (default 60 seconds).</summary>
    TimeSpan PollInterval { get; set; }

    /// <summary>
    /// Start polling. Called at process start and after a PAT is saved.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop polling. Called on process Exit and when the PAT is cleared.
    /// Not called when the app is backgrounded (ADR-010).
    /// </summary>
    void Stop();

    /// <summary>Trigger an immediate poll (manual refresh).</summary>
    Task RefreshAsync();
}
