using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class NotificationsViewModelTests
{
    private static string NotificationsJson(params (string id, bool unread, string reason, string type, string title)[] items)
    {
        var json = items.Select(i =>
            $"{{\"id\":\"{i.id}\",\"unread\":{i.unread.ToString().ToLower()}," +
            $"\"reason\":\"{i.reason}\"," +
            $"\"updated_at\":\"2025-01-01T00:00:00Z\"," +
            $"\"url\":\"https://api.github.com/notifications/threads/{i.id}\"," +
            $"\"subject\":{{\"title\":\"{i.title}\",\"type\":\"{i.type}\"," +
            $"\"url\":\"https://api.github.com/repos/o/r/issues/1\"," +
            $"\"latest_comment_url\":\"https://github.com/o/r/issues/1#issuecomment-1\"}}," +
            $"\"repository\":{{\"id\":1,\"name\":\"r\",\"full_name\":\"o/r\"," +
            $"\"html_url\":\"https://github.com/o/r\"}}}}");
        return $"[{string.Join(",", json)}]";
    }

    [Fact]
    public void Constructor_SubscribesToPollerEvents()
    {
        var poller = new FakeNotificationPoller();
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        // Simulate a poll — the ViewModel should update its state.
        var notifications = new[]
        {
            new Notification { Id = "1", Unread = true, Reason = "mention",
                Subject = new NotificationSubject { Title = "Test", Type = "Issue" },
                Repository = new NotificationRepository { FullName = "o/r" } },
        };
        poller.SimulateNotifications(notifications, 1);

        Assert.Single(vm.Notifications);
        Assert.Equal(1, vm.UnreadCount.Value);
        vm.Dispose();
    }

    [Fact]
    public void StartPolling_CallsPollerStart()
    {
        var poller = new FakeNotificationPoller();
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        vm.StartPollingCommand.Execute(null);

        Assert.Equal(1, poller.StartCallCount);
        Assert.True(vm.IsPolling.Value);
        vm.Dispose();
    }

    [Fact]
    public void StopPolling_CallsPollerStop()
    {
        var poller = new FakeNotificationPoller();
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        vm.StartPollingCommand.Execute(null);
        vm.StopPollingCommand.Execute(null);

        Assert.Equal(1, poller.StopCallCount);
        Assert.False(vm.IsPolling.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Refresh_CallsPollerRefresh()
    {
        var poller = new FakeNotificationPoller();
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, poller.RefreshCallCount);
        Assert.False(vm.IsBusy.Value);
        vm.Dispose();
    }

    [Fact]
    public void NotificationsUpdated_WithEmptyArray_ClearsNotifications()
    {
        var poller = new FakeNotificationPoller();
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        // First add some notifications.
        poller.SimulateNotifications(
            [new Notification { Id = "1", Unread = true }], 1);
        Assert.Single(vm.Notifications);

        // Then simulate an empty poll.
        poller.SimulateNotifications([], 0);

        Assert.Empty(vm.Notifications);
        Assert.Equal(0, vm.UnreadCount.Value);
        vm.Dispose();
    }

    [Fact]
    public void NotificationsUpdated_WithMultipleNotifications_PopulatesList()
    {
        var poller = new FakeNotificationPoller();
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        var notifications = new[]
        {
            new Notification { Id = "1", Unread = true, Reason = "mention",
                Subject = new NotificationSubject { Title = "Issue 1", Type = "Issue" } },
            new Notification { Id = "2", Unread = true, Reason = "author",
                Subject = new NotificationSubject { Title = "PR 2", Type = "PullRequest" } },
            new Notification { Id = "3", Unread = false, Reason = "assign",
                Subject = new NotificationSubject { Title = "Issue 3", Type = "Issue" } },
        };
        poller.SimulateNotifications(notifications, 2);

        Assert.Equal(3, vm.Notifications.Count);
        Assert.Equal(2, vm.UnreadCount.Value);
        Assert.Equal("Issue 1", vm.Notifications[0].Subject.Title);
        vm.Dispose();
    }

    [Fact]
    public async Task MarkAsRead_WithToken_RemovesNotificationAndDecrementsCount()
    {
        var handler = new MockHttpHandler()
            .When("/notifications/threads/1", _ => new MockResponse("{}"));
        var factory = new FakeGitHubClientFactory(handler);
        var poller = new FakeNotificationPoller();
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        var notification = new Notification
        {
            Id = "1",
            Unread = true,
            Subject = new NotificationSubject { Title = "Test", Type = "Issue" },
        };
        poller.SimulateNotifications([notification], 1);

        await vm.MarkAsReadCommand.ExecuteAsync(notification);

        Assert.Empty(vm.Notifications);
        Assert.Equal(0, vm.UnreadCount.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task MarkAsRead_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var poller = new FakeNotificationPoller();
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        var notification = new Notification { Id = "1", Unread = true };
        poller.SimulateNotifications([notification], 1);

        await vm.MarkAsReadCommand.ExecuteAsync(notification);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Single(vm.Notifications); // Not removed
        vm.Dispose();
    }

    [Fact]
    public async Task MarkAllAsRead_WithToken_ClearsUnreadNotifications()
    {
        var handler = new MockHttpHandler()
            .When("/notifications", _ => new MockResponse("{}"));
        var factory = new FakeGitHubClientFactory(handler);
        var poller = new FakeNotificationPoller();
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        var notifications = new[]
        {
            new Notification { Id = "1", Unread = true },
            new Notification { Id = "2", Unread = true },
            new Notification { Id = "3", Unread = false },
        };
        poller.SimulateNotifications(notifications, 2);

        await vm.MarkAllAsReadCommand.ExecuteAsync(null);

        // Unread notifications removed, read ones stay.
        Assert.Single(vm.Notifications);
        Assert.Equal(0, vm.UnreadCount.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task MarkAllAsRead_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var poller = new FakeNotificationPoller();
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        poller.SimulateNotifications(
            [new Notification { Id = "1", Unread = true }], 1);

        await vm.MarkAllAsReadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Single(vm.Notifications); // Not cleared
        vm.Dispose();
    }

    [Fact]
    public void Dispose_UnsubscribesFromPollerEvents()
    {
        var poller = new FakeNotificationPoller();
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var vm = new NotificationsViewModel(factory, poller, new FakeBrowserLauncher());

        vm.Dispose();

        // After dispose, simulating notifications should not update the VM.
        poller.SimulateNotifications(
            [new Notification { Id = "1", Unread = true }], 1);

        // The VM's Notifications should still be empty (event was unsubscribed).
        Assert.Empty(vm.Notifications);
    }
}
