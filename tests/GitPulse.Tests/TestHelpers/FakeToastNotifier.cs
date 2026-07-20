using GitPulse.Core.Abstractions;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// <see cref="IToastNotifier"/> stub that records summary Toast calls instead of
/// showing an OS Toast.
/// </summary>
public sealed class FakeToastNotifier : IToastNotifier
{
    public List<int> SummaryNewCounts { get; } = [];

    public void ShowNewNotificationsSummary(int newCount) => SummaryNewCounts.Add(newCount);
}
