using GitPulse.Core.Abstractions;

namespace GitPulse.App.Platforms.Android;

/// <summary>
/// Android no-op <see cref="IToastNotifier"/>. See ADR-010.
/// </summary>
public sealed class AndroidToastNotifier : IToastNotifier
{
    /// <inheritdoc />
    public void ShowNewNotificationsSummary(int newCount)
    {
        // No OS Toast on Android for this slice.
    }
}
