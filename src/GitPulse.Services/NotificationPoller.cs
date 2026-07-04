using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.Services;

/// <summary>
/// Polls GitHub notifications on a timer using R3 <see cref="Observable"/>.
/// Interval and publishes results via <see cref="INotificationPoller.NotificationsUpdated"/>.
/// This is the M4 Events domain showcase: a timer-driven reactive stream
/// that simulates realtime notification delivery via polling.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reactive pipeline (internal):</b>
/// <code>
/// Observable.Interval(PollInterval)
///   .StartWith(0)              // emit immediately on Start
///   .SubscribeAwait(PollAsync) // fetch notifications on each tick
/// </code>
/// </para>
/// <para>
/// The poller handles auth gracefully: if no token is configured, it
/// fires <see cref="INotificationPoller.NotificationsUpdated"/> with an
/// empty array and unread count 0.
/// </para>
/// </remarks>
public sealed class NotificationPoller : INotificationPoller
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private IDisposable? _pollSubscription;
    private bool _isPolling;
    private bool _disposed;

    public event Action<Notification[], int>? NotificationsUpdated;

    public int UnreadCount { get; private set; }

    public bool IsPolling
    {
        get => _isPolling;
        private set
        {
            if (_isPolling != value)
            {
                _isPolling = value;
                IsPollingChanged?.Invoke(value);
            }
        }
    }

    public event Action<bool>? IsPollingChanged;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Creates the poller with the default <see cref="TimeProvider.System"/>.
    /// </summary>
    public NotificationPoller(IGitHubClientFactory clientFactory)
        : this(clientFactory, TimeProvider.System)
    {
    }

    /// <summary>
    /// Creates the poller with a custom <see cref="TimeProvider"/> (for testing).
    /// </summary>
    public NotificationPoller(IGitHubClientFactory clientFactory, TimeProvider timeProvider)
    {
        _clientFactory = clientFactory;
        _timeProvider = timeProvider;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isPolling || _disposed)
                return;

            IsPolling = true;

            // R3 Observable.Interval emits Unit values at each interval.
            // Prepend(Unit.Default) triggers an immediate poll on Start
            // (before the first interval tick).
            _pollSubscription = Observable
                .Interval(PollInterval, _timeProvider)
                .Prepend(Unit.Default)
                .SubscribeAwait(async (_, ct) =>
                {
                    await PollAsync(ct);
                });
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isPolling)
                return;

            IsPolling = false;
            _pollSubscription?.Dispose();
            _pollSubscription = null;
        }
    }

    public async Task RefreshAsync()
    {
        await PollAsync(CancellationToken.None);
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            var client = await _clientFactory.CreateClientAsync(ct);
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                OnNotificationsUpdated([], 0);
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var notifications = await api.ListNotifications().FirstAsync(cts.Token);
            var unread = notifications.Count(n => n.Unread);
            OnNotificationsUpdated(notifications, unread);
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation — don't update, keep last state.
        }
        catch (Exception)
        {
            // Network/API error — don't crash the poller, just skip this cycle.
        }
    }

    private void OnNotificationsUpdated(Notification[] notifications, int unreadCount)
    {
        UnreadCount = unreadCount;
        NotificationsUpdated?.Invoke(notifications, unreadCount);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
