using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.Services;
using Xunit;

namespace GitPulse.Tests;

public class NotificationPollerTests
{
    [Fact]
    public void Start_SetsIsPollingTrue()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var poller = new NotificationPoller(factory);

        bool? isPollingChanged = null;
        poller.IsPollingChanged += v => isPollingChanged = v;

        poller.Start();

        Assert.True(poller.IsPolling);
        Assert.True(isPollingChanged);
    }

    [Fact]
    public void Stop_SetsIsPollingFalse()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var poller = new NotificationPoller(factory);

        poller.Start();
        bool? isPollingChanged = null;
        poller.IsPollingChanged += v => isPollingChanged = v;

        poller.Stop();

        Assert.False(poller.IsPolling);
        Assert.False(isPollingChanged);
    }

    [Fact]
    public void Start_CalledTwice_DoesNotRestart()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var poller = new NotificationPoller(factory);

        poller.Start();
        poller.Start(); // Should be a no-op

        Assert.True(poller.IsPolling);
    }

    [Fact]
    public void Stop_WhenNotPolling_IsNoOp()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var poller = new NotificationPoller(factory);

        poller.Stop(); // Should not throw

        Assert.False(poller.IsPolling);
    }

    [Fact]
    public async Task RefreshAsync_WithoutToken_FiresEmptyNotifications()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var poller = new NotificationPoller(factory);

        Notification[]? received = null;
        int? receivedUnread = null;
        poller.NotificationsUpdated += (n, u) =>
        {
            received = n;
            receivedUnread = u;
        };

        await poller.RefreshAsync();

        Assert.NotNull(received);
        Assert.Empty(received!);
        Assert.Equal(0, receivedUnread);
        Assert.Equal(0, poller.UnreadCount);
        poller.Dispose();
    }

    [Fact]
    public async Task RefreshAsync_WithToken_FiresNotificationsFromApi()
    {
        var notificationsJson =
            "[{\"id\":\"1\",\"unread\":true,\"reason\":\"mention\"," +
            "\"updated_at\":\"2025-01-01T00:00:00Z\"," +
            "\"url\":\"https://api.github.com/notifications/threads/1\"," +
            "\"subject\":{\"title\":\"Test issue\",\"type\":\"Issue\"," +
            "\"url\":\"https://api.github.com/repos/o/r/issues/1\"}," +
            "\"repository\":{\"id\":1,\"name\":\"r\",\"full_name\":\"o/r\"," +
            "\"html_url\":\"https://github.com/o/r\"}}]";
        var handler = new MockHttpHandler()
            .When("/notifications", notificationsJson);
        var factory = new FakeGitHubClientFactory(handler);
        var poller = new NotificationPoller(factory);

        Notification[]? received = null;
        int? receivedUnread = null;
        poller.NotificationsUpdated += (n, u) =>
        {
            received = n;
            receivedUnread = u;
        };

        await poller.RefreshAsync();

        Assert.NotNull(received);
        Assert.Single(received!);
        Assert.Equal("1", received![0].Id);
        Assert.True(received[0].Unread);
        Assert.Equal("Test issue", received[0].Subject.Title);
        Assert.Equal("Issue", received[0].Subject.Type);
        Assert.Equal("o/r", received[0].Repository.FullName);
        Assert.Equal(1, receivedUnread);
        Assert.Equal(1, poller.UnreadCount);
        poller.Dispose();
    }

    [Fact]
    public async Task RefreshAsync_WithApiError_DoesNotFireEvent()
    {
        // No mock route → 404 → exception caught internally.
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var poller = new NotificationPoller(factory);

        bool fired = false;
        poller.NotificationsUpdated += (_, _) => fired = true;

        await poller.RefreshAsync();

        // The 404 causes an exception which is caught — no event fired.
        Assert.False(fired);
        poller.Dispose();
    }

    [Fact]
    public void Dispose_StopsPolling()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var poller = new NotificationPoller(factory);

        poller.Start();
        poller.Dispose();

        Assert.False(poller.IsPolling);
    }

    [Fact]
    public void PollInterval_DefaultIs60Seconds()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var poller = new NotificationPoller(factory);

        Assert.Equal(TimeSpan.FromSeconds(60), poller.PollInterval);
        poller.Dispose();
    }
}

public class NotificationModelTests
{
    [Fact]
    public void Notification_Defaults_AreValid()
    {
        var notification = new Notification { Id = "123" };

        Assert.Equal("123", notification.Id);
        Assert.False(notification.Unread);
        Assert.Equal(string.Empty, notification.Reason);
        Assert.Equal(string.Empty, notification.Url);
        Assert.NotNull(notification.Subject);
        Assert.NotNull(notification.Repository);
    }

    [Fact]
    public void NotificationSubject_Defaults_AreValid()
    {
        var subject = new NotificationSubject { Title = "Test" };

        Assert.Equal("Test", subject.Title);
        Assert.Equal(string.Empty, subject.Type);
        Assert.Equal(string.Empty, subject.Url);
        Assert.Null(subject.LatestCommentUrl);
    }

    [Fact]
    public void NotificationRepository_Defaults_AreValid()
    {
        var repo = new NotificationRepository { Name = "myrepo" };

        Assert.Equal("myrepo", repo.Name);
        Assert.Equal(string.Empty, repo.FullName);
        Assert.Equal(string.Empty, repo.HtmlUrl);
        Assert.Equal(0, repo.Id);
    }

    [Fact]
    public void Notification_WithAllFields_PreservesValues()
    {
        var notification = new Notification
        {
            Id = "abc123",
            Unread = true,
            Reason = "mention",
            UpdatedAt = new DateTime(2025, 7, 1),
            Url = "https://api.github.com/notifications/threads/abc123",
            Subject = new NotificationSubject
            {
                Title = "Bug report",
                Type = "Issue",
                Url = "https://api.github.com/repos/o/r/issues/42",
                LatestCommentUrl = "https://github.com/o/r/issues/42#issuecomment-1",
            },
            Repository = new NotificationRepository
            {
                Id = 100,
                Name = "repo",
                FullName = "owner/repo",
                HtmlUrl = "https://github.com/owner/repo",
            },
        };

        Assert.Equal("abc123", notification.Id);
        Assert.True(notification.Unread);
        Assert.Equal("mention", notification.Reason);
        Assert.Equal("Bug report", notification.Subject.Title);
        Assert.Equal("Issue", notification.Subject.Type);
        Assert.Equal("owner/repo", notification.Repository.FullName);
    }
}
