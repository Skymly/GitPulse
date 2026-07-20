namespace GitPulse.Core.Abstractions;

/// <summary>
/// Shows an OS Toast for New Notifications. Platform implementations live under
/// App/Platforms; Android may be a no-op.
/// </summary>
/// <remarks>
/// See ADR-010 and CONTEXT.md (<c>Toast</c>, <c>New Notification</c>).
/// Callers must already have decided that a Toast is appropriate (hidden window,
/// non-empty new-id set, not the baseline snapshot).
/// </remarks>
public interface IToastNotifier
{
    /// <summary>
    /// Shows at most one summary Toast describing <paramref name="newCount"/>
    /// New Notifications from a single poll cycle.
    /// </summary>
    /// <param name="newCount">Count of notification ids not present in the previous snapshot. Must be positive.</param>
    void ShowNewNotificationsSummary(int newCount);
}
