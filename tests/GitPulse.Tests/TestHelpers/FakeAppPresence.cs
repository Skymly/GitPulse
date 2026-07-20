using GitPulse.Core.Abstractions;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// <see cref="IAppPresence"/> stub with a mutable visibility flag for
/// <see cref="GitPulse.Core.Notifications.NotificationToastCoordinator"/> tests.
/// </summary>
public sealed class FakeAppPresence : IAppPresence
{
    public bool IsMainWindowVisible { get; set; } = true;
}
