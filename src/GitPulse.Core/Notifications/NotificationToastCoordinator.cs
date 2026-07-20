using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;

namespace GitPulse.Core.Notifications;

/// <summary>
/// Decides when to show a summary Toast for New Notifications given poll
/// results and main-window visibility. Testable without a UI host.
/// </summary>
/// <remarks>
/// <para>
/// Rules (ADR-010 / CONTEXT.md):
/// </para>
/// <list type="bullet">
/// <item>The first snapshot establishes a baseline and never emits a Toast.</item>
/// <item>While the main window is visible, polls never emit Toasts (ids are still tracked).</item>
/// <item>While hidden, a poll with one or more new ids emits exactly one summary Toast per cycle.</item>
/// <item>“New” means an id absent from the previous snapshot (id-set diff).</item>
/// </list>
/// </remarks>
public sealed class NotificationToastCoordinator
{
    private readonly IAppPresence _presence;
    private readonly IToastNotifier _toastNotifier;
    private HashSet<string> _knownIds = new(StringComparer.Ordinal);
    private bool _hasBaseline;

    public NotificationToastCoordinator(IAppPresence presence, IToastNotifier toastNotifier)
    {
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentNullException.ThrowIfNull(toastNotifier);
        _presence = presence;
        _toastNotifier = toastNotifier;
    }

    /// <summary>
    /// Processes one poll (or refresh) result. Updates the known-id snapshot and
    /// may show a single summary Toast when appropriate.
    /// </summary>
    public void HandleNotificationsUpdated(IReadOnlyList<Notification> notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        var currentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var notification in notifications)
        {
            if (!string.IsNullOrEmpty(notification.Id))
            {
                currentIds.Add(notification.Id);
            }
        }

        if (!_hasBaseline)
        {
            _knownIds = currentIds;
            _hasBaseline = true;
            return;
        }

        var newCount = 0;
        foreach (var id in currentIds)
        {
            if (!_knownIds.Contains(id))
            {
                newCount++;
            }
        }

        _knownIds = currentIds;

        if (newCount == 0 || _presence.IsMainWindowVisible)
        {
            return;
        }

        _toastNotifier.ShowNewNotificationsSummary(newCount);
    }

    /// <summary>
    /// Clears the known-id snapshot so the next poll is treated as a baseline
    /// (no Toast). Useful when entering Tray Presence if the host wants to
    /// suppress Toasts for already-seen items on the first hidden poll.
    /// </summary>
    public void ResetBaseline()
    {
        _knownIds = new HashSet<string>(StringComparer.Ordinal);
        _hasBaseline = false;
    }
}
