using GitPulse.Core.Models;
using GitPulse.Core.Notifications;
using GitPulse.Tests.TestHelpers;
using Xunit;

namespace GitPulse.Tests;

public class NotificationToastCoordinatorTests
{
    [Fact]
    public void FirstSnapshot_IsBaseline_NeverToasts()
    {
        var presence = new FakeAppPresence { IsMainWindowVisible = false };
        var toast = new FakeToastNotifier();
        var coordinator = new NotificationToastCoordinator(presence, toast);

        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
        ]);

        Assert.Empty(toast.SummaryNewCounts);
    }

    [Fact]
    public void Hidden_WithNewIds_EmitsOneSummaryToast()
    {
        var presence = new FakeAppPresence { IsMainWindowVisible = false };
        var toast = new FakeToastNotifier();
        var coordinator = new NotificationToastCoordinator(presence, toast);

        coordinator.HandleNotificationsUpdated([NotificationWithId("1")]);
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
        ]);

        Assert.Equal([1], toast.SummaryNewCounts);
    }

    [Fact]
    public void Hidden_WithNoNewIds_DoesNotToast()
    {
        var presence = new FakeAppPresence { IsMainWindowVisible = false };
        var toast = new FakeToastNotifier();
        var coordinator = new NotificationToastCoordinator(presence, toast);

        coordinator.HandleNotificationsUpdated([NotificationWithId("1")]);
        coordinator.HandleNotificationsUpdated([NotificationWithId("1")]);

        Assert.Empty(toast.SummaryNewCounts);
    }

    [Fact]
    public void Visible_WithNewIds_DoesNotToast_ButTracksIds()
    {
        var presence = new FakeAppPresence { IsMainWindowVisible = true };
        var toast = new FakeToastNotifier();
        var coordinator = new NotificationToastCoordinator(presence, toast);

        coordinator.HandleNotificationsUpdated([NotificationWithId("1")]);
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
        ]);

        Assert.Empty(toast.SummaryNewCounts);

        // After becoming hidden, id "2" is already known — no toast for it.
        presence.IsMainWindowVisible = false;
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
        ]);

        Assert.Empty(toast.SummaryNewCounts);

        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
            NotificationWithId("3"),
        ]);

        Assert.Equal([1], toast.SummaryNewCounts);
    }

    [Fact]
    public void Hidden_MultipleNewIds_StillOneToastWithCount()
    {
        var presence = new FakeAppPresence { IsMainWindowVisible = false };
        var toast = new FakeToastNotifier();
        var coordinator = new NotificationToastCoordinator(presence, toast);

        coordinator.HandleNotificationsUpdated([NotificationWithId("1")]);
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
            NotificationWithId("3"),
            NotificationWithId("4"),
        ]);

        Assert.Equal([3], toast.SummaryNewCounts);
    }

    [Fact]
    public void SubsequentCycle_OverlappingIds_OnlyCountsTrulyNew()
    {
        var presence = new FakeAppPresence { IsMainWindowVisible = false };
        var toast = new FakeToastNotifier();
        var coordinator = new NotificationToastCoordinator(presence, toast);

        coordinator.HandleNotificationsUpdated([NotificationWithId("1")]);
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
        ]);
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("2"),
            NotificationWithId("3"),
        ]);

        Assert.Equal([1, 1], toast.SummaryNewCounts);
    }

    [Fact]
    public void ResetBaseline_NextPollIsBaselineOnly()
    {
        var presence = new FakeAppPresence { IsMainWindowVisible = false };
        var toast = new FakeToastNotifier();
        var coordinator = new NotificationToastCoordinator(presence, toast);

        coordinator.HandleNotificationsUpdated([NotificationWithId("1")]);
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("1"),
            NotificationWithId("2"),
        ]);
        Assert.Equal([1], toast.SummaryNewCounts);

        coordinator.ResetBaseline();
        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("2"),
            NotificationWithId("3"),
        ]);

        Assert.Equal([1], toast.SummaryNewCounts);

        coordinator.HandleNotificationsUpdated(
        [
            NotificationWithId("2"),
            NotificationWithId("3"),
            NotificationWithId("4"),
        ]);

        Assert.Equal([1, 1], toast.SummaryNewCounts);
    }

    private static Notification NotificationWithId(string id) => new() { Id = id };
}
