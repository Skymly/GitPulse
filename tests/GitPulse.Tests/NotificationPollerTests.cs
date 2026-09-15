using System.Net;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.Services;
using GitPulse.Tests.TestHelpers;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace GitPulse.Tests;

public class NotificationPollerTests
{
    private static string NotificationsJson(string id = "1") =>
        "[{\"id\":\"" + id + "\",\"unread\":true,\"reason\":\"mention\"," +
        "\"updated_at\":\"2025-01-01T00:00:00Z\"," +
        "\"url\":\"https://api.github.com/notifications/threads/" + id + "\"," +
        "\"subject\":{\"title\":\"Test issue\",\"type\":\"Issue\"," +
        "\"url\":\"https://api.github.com/repos/o/r/issues/1\"}," +
        "\"repository\":{\"id\":1,\"name\":\"r\",\"full_name\":\"o/r\"," +
        "\"html_url\":\"https://github.com/o/r\"}}]";

    [Fact]
    public void LastError_IsExposedOnINotificationPoller()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        using var poller = new NotificationPoller(factory);
        INotificationPoller asInterface = poller;

        Assert.Null(asInterface.LastError);
    }

    [Fact]
    public void Start_SetsIsPollingTrue()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        using var poller = new NotificationPoller(factory);

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
        using var poller = new NotificationPoller(factory);

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
        using var poller = new NotificationPoller(factory);

        poller.Start();
        poller.Start();

        Assert.True(poller.IsPolling);
    }

    [Fact]
    public void Stop_WhenNotPolling_IsNoOp()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        using var poller = new NotificationPoller(factory);

        poller.Stop();

        Assert.False(poller.IsPolling);
    }

    [Fact]
    public async Task RefreshAsync_WithoutToken_FiresEmptyNotifications()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        using var poller = new NotificationPoller(factory);

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
    }

    [Fact]
    public async Task RefreshAsync_WithToken_FiresNotificationsFromApi()
    {
        var handler = new MockHttpHandler()
            .When("/notifications", NotificationsJson());
        var factory = new FakeGitHubClientFactory(handler);
        using var poller = new NotificationPoller(factory);

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
    }

    [Fact]
    public async Task RefreshAsync_WithApiError_DoesNotFireEvent()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        using var poller = new NotificationPoller(factory);

        var fired = false;
        poller.NotificationsUpdated += (_, _) => fired = true;

        await poller.RefreshAsync();

        Assert.False(fired);
        Assert.NotNull(poller.LastError);
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
        using var poller = new NotificationPoller(factory);

        Assert.Equal(TimeSpan.FromSeconds(60), poller.PollInterval);
    }

    [Fact]
    public async Task Start_WithFakeTime_PollsImmediatelyThenOnEachInterval()
    {
        var polls = 0;
        var handler = new MockHttpHandler()
            .When("/notifications", _ =>
            {
                polls++;
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var time = new FakeTimeProvider();
        using var poller = new NotificationPoller(factory, time);
        poller.PollInterval = TimeSpan.FromSeconds(10);

        poller.Start();
        await AsyncTestWait.UntilAsync(() => polls == 1);

        time.Advance(TimeSpan.FromSeconds(10));
        await AsyncTestWait.UntilAsync(() => polls == 2);

        time.Advance(TimeSpan.FromSeconds(10));
        await AsyncTestWait.UntilAsync(() => polls == 3);
    }

    [Fact]
    public async Task Start_Unauthorized_SetsLastErrorAndSkipsUntilBackoff()
    {
        var polls = 0;
        var handler = new MockHttpHandler()
            .When("/notifications", _ =>
            {
                polls++;
                return new MockResponse(
                    "{\"message\":\"Bad credentials\"}",
                    StatusCode: HttpStatusCode.Unauthorized,
                    AttachRequest: true);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var time = new FakeTimeProvider();
        using var poller = new NotificationPoller(factory, time);
        poller.PollInterval = TimeSpan.FromSeconds(10);

        string? lastError = null;
        poller.LastErrorChanged += e => lastError = e;
        var fired = false;
        poller.NotificationsUpdated += (_, _) => fired = true;

        poller.Start();
        await AsyncTestWait.UntilAsync(() => polls == 1 && lastError is not null);

        Assert.False(fired);
        Assert.Contains("401", lastError, StringComparison.Ordinal);

        time.Advance(TimeSpan.FromSeconds(10));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, polls);

        time.Advance(TimeSpan.FromSeconds(20));
        await AsyncTestWait.UntilAsync(() => polls == 2);
    }

    [Fact]
    public async Task Start_HonoursRetryAfterBeforeNextPoll()
    {
        var polls = 0;
        var handler = new MockHttpHandler()
            .When("/notifications", _ =>
            {
                polls++;
                return new MockResponse(
                    "{\"message\":\"API rate limit exceeded\"}",
                    StatusCode: HttpStatusCode.Forbidden,
                    AttachRequest: true,
                    RetryAfter: "30");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var time = new FakeTimeProvider();
        using var poller = new NotificationPoller(factory, time);
        poller.PollInterval = TimeSpan.FromSeconds(10);

        poller.Start();
        await AsyncTestWait.UntilAsync(() => polls == 1 && poller.LastError is not null);

        time.Advance(TimeSpan.FromSeconds(20));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, polls);

        time.Advance(TimeSpan.FromSeconds(15));
        await AsyncTestWait.UntilAsync(() => polls == 2);
    }

    [Fact]
    public async Task RefreshAsync_FollowsLinkNext_ConcatenatesPages()
    {
        var polls = 0;
        var handler = new MockHttpHandler()
            .When("/notifications", req =>
            {
                polls++;
                var query = req.RequestUri?.Query ?? "";
                if (query.Contains("page=2", StringComparison.Ordinal))
                    return new MockResponse(NotificationsJson("2"));
                return new MockResponse(
                    NotificationsJson("1"),
                    "<https://api.github.com/notifications?page=2>; rel=\"next\"");
            });
        var factory = new FakeGitHubClientFactory(handler);
        using var poller = new NotificationPoller(factory);

        Notification[]? received = null;
        int? unread = null;
        poller.NotificationsUpdated += (n, u) =>
        {
            received = n;
            unread = u;
        };

        await poller.RefreshAsync();

        Assert.Equal(2, polls);
        Assert.NotNull(received);
        Assert.Equal(["1", "2"], received!.Select(n => n.Id).ToArray());
        Assert.Equal(2, unread);
        Assert.Equal(2, poller.UnreadCount);
    }

    [Fact]
    public async Task RefreshAsync_StopsAfterTenPages_WhenLinkRemains()
    {
        var polls = 0;
        var handler = new MockHttpHandler()
            .When("/notifications", req =>
            {
                polls++;
                var id = polls.ToString();
                return new MockResponse(
                    NotificationsJson(id),
                    $"<https://api.github.com/notifications?page={polls + 1}>; rel=\"next\"");
            });
        var factory = new FakeGitHubClientFactory(handler);
        using var poller = new NotificationPoller(factory);

        Notification[]? received = null;
        poller.NotificationsUpdated += (n, _) => received = n;

        await poller.RefreshAsync();

        Assert.Equal(10, polls);
        Assert.NotNull(received);
        Assert.Equal(10, received!.Length);
        Assert.Equal("10", received[^1].Id);
    }

    [Fact]
    public async Task RefreshAsync_WhenLaterPageFails_DoesNotPublishPartialSnapshot()
    {
        var handler = new MockHttpHandler()
            .When("/notifications", req =>
            {
                var query = req.RequestUri?.Query ?? "";
                if (query.Contains("page=2", StringComparison.Ordinal))
                {
                    return new MockResponse(
                        "{\"message\":\"Bad credentials\"}",
                        StatusCode: HttpStatusCode.Unauthorized,
                        AttachRequest: true);
                }

                return new MockResponse(
                    NotificationsJson("1"),
                    "<https://api.github.com/notifications?page=2>; rel=\"next\"");
            });
        var factory = new FakeGitHubClientFactory(handler);
        using var poller = new NotificationPoller(factory);

        var fired = false;
        string? lastError = null;
        poller.NotificationsUpdated += (_, _) => fired = true;
        poller.LastErrorChanged += e => lastError = e;

        await poller.RefreshAsync();

        Assert.False(fired);
        Assert.Contains("401", lastError, StringComparison.Ordinal);
        Assert.Equal(0, poller.UnreadCount);
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
