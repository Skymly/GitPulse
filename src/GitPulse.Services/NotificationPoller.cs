using System.Globalization;
using System.Net.Http.Headers;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
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
/// empty array and unread count 0, then <see cref="Stop"/>s so the timer
/// does not keep ticking. Authenticated polls follow
/// <c>Link: rel="next"</c> up to 10 pages so the unread badge is not
/// truncated at GitHub's first page.
/// </para>
/// <para>
/// HTTP failures set <see cref="LastError"/> and skip the next ticks until
/// <c>Retry-After</c> or an exponential backoff elapses. A successful poll
/// clears the error. <see cref="RefreshAsync"/> polls immediately and
/// shares the run cancellation token with <see cref="Stop"/> /
/// <see cref="Dispose"/>. A refresh that arrives while a poll is in
/// flight is queued for the next round. Completed polls do not publish
/// after dispose or cancellation.
/// <see cref="Stop"/> always publishes an empty snapshot so the unread
/// badge clears when polling ends or the PAT is removed.
/// </para>
/// </remarks>
public sealed class NotificationPoller : INotificationPoller
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);
    private const int MaxNotificationPages = 10;

    private readonly IGitHubClientFactory _clientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private IDisposable? _pollSubscription;
    private CancellationTokenSource _cts = new();
    private Task? _drain;
    private bool _isPolling;
    private bool _disposed;
    private int _busy;
    private int _refreshQueued;
    private int _failures;
    private DateTimeOffset _notBefore;
    private string? _lastError;

    public event Action<Notification[], int>? NotificationsUpdated;

    public event Action<bool>? IsPollingChanged;

    public event Action<string?>? LastErrorChanged;

    public int UnreadCount { get; private set; }

    public bool IsPolling => _isPolling;

    /// <summary>Last poll failure; null after a successful poll or unauthenticated stop.</summary>
    public string? LastError => _lastError;

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
        var raised = false;
        lock (_lock)
        {
            if (_isPolling || _disposed)
                return;

            EnsureRunTokenLocked();
            _isPolling = true;
            raised = true;
        }

        if (raised)
            IsPollingChanged?.Invoke(true);

        // Subscribe outside the lock so a synchronous first poll that
        // Stop()s (no token) cannot deadlock on this lock.
        var subscription = Observable
            .Interval(PollInterval, _timeProvider)
            .Prepend(Unit.Default)
            .SubscribeAwait(async (_, ct) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, GetRunToken());
                await PollAsync(linked.Token, force: false);
            });

        lock (_lock)
        {
            if (_disposed || !_isPolling)
            {
                subscription.Dispose();
                return;
            }

            _pollSubscription = subscription;
        }
    }

    public void Stop()
    {
        IDisposable? subscription = null;
        var raised = false;
        lock (_lock)
        {
            if (_isPolling)
            {
                _isPolling = false;
                raised = true;
                subscription = _pollSubscription;
                _pollSubscription = null;
            }
        }

        subscription?.Dispose();
        if (raised)
            IsPollingChanged?.Invoke(false);

        CancelRun();
        UnreadCount = 0;
        NotificationsUpdated?.Invoke([], 0);
    }

    public Task RefreshAsync()
    {
        lock (_lock)
        {
            if (_disposed)
                return Task.CompletedTask;

            _refreshQueued = 1;
            if (_drain is { IsCompleted: false })
                return _drain;

            _drain = DrainRefreshAsync();
            return _drain;
        }
    }

    private async Task DrainRefreshAsync()
    {
        await Task.Yield();
        var token = EnsureRunToken();
        while (!_disposed && !token.IsCancellationRequested)
        {
            lock (_lock)
            {
                if (_refreshQueued == 0)
                {
                    _drain = null;
                    return;
                }

                _refreshQueued = 0;
            }

            while (Interlocked.Exchange(ref _busy, 1) != 0)
            {
                try
                {
                    await WaitUntilIdleAsync(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_disposed || token.IsCancellationRequested)
                    return;
            }

            try
            {
                await PollAsync(token, force: true, holdBusy: true);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }
    }

    private async Task PollAsync(CancellationToken ct, bool force, bool holdBusy = false)
    {
        if (!force)
        {
            DateTimeOffset notBefore;
            lock (_lock)
                notBefore = _notBefore;
            if (_timeProvider.GetUtcNow() < notBefore)
                return;
        }

        if (!holdBusy && Interlocked.Exchange(ref _busy, 1) != 0)
            return;

        Notification[]? snapshot = null;
        var unread = 0;
        string? error = null;
        var stop = false;
        TimeSpan? wait = null;

        try
        {
            using var session = await _clientFactory.CreatePagedSessionAsync(ct);
            var client = session.Client;
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                snapshot = [];
                unread = 0;
                stop = true;
            }
            else
            {
                var api = RestService.For<IGitHubReposApi>(client);
                var notifications = await ListAllNotificationsAsync(api, session, ct);
                snapshot = notifications;
                unread = notifications.Count(n => n.Unread);
            }
        }
        catch (OperationCanceledException) when (_disposed || ct.IsCancellationRequested)
        {
            // Stop / dispose cancelled the run.
        }
        catch (OperationCanceledException)
        {
            error = "Request timed out.";
            wait = NextBackoff();
        }
        catch (ApiException ex)
        {
            error = FormatApiError(ex);
            wait = ReadRetryAfter(ex) ?? NextBackoff();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            wait = NextBackoff();
        }
        finally
        {
            if (!holdBusy)
                Interlocked.Exchange(ref _busy, 0);
        }

        if (stop && !_disposed)
            Stop();

        if (_disposed || ct.IsCancellationRequested)
            return;

        if (snapshot is not null)
        {
            UnreadCount = unread;
            NotificationsUpdated?.Invoke(snapshot, unread);
            ClearFailures();
            SetLastError(null);
        }
        else if (error is not null)
        {
            RecordFailure(wait ?? NextBackoff());
            SetLastError(error);
        }
    }

    private CancellationToken EnsureRunToken()
    {
        lock (_lock)
            return EnsureRunTokenLocked();
    }

    private CancellationToken EnsureRunTokenLocked()
    {
        if (_disposed)
            return new CancellationToken(canceled: true);

        if (_cts.IsCancellationRequested)
        {
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        return _cts.Token;
    }

    private CancellationToken GetRunToken()
    {
        lock (_lock)
            return _cts.Token;
    }

    private void CancelRun()
    {
        lock (_lock)
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
    }

    private async Task WaitUntilIdleAsync(CancellationToken token)
    {
        while (Volatile.Read(ref _busy) != 0)
            await Task.Delay(10, token);
    }

    private void ClearFailures()
    {
        lock (_lock)
        {
            _failures = 0;
            _notBefore = default;
        }
    }

    private void RecordFailure(TimeSpan wait)
    {
        lock (_lock)
        {
            _failures++;
            var delay = wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
            if (delay > MaxBackoff)
                delay = MaxBackoff;
            _notBefore = _timeProvider.GetUtcNow() + delay;
        }
    }

    private TimeSpan NextBackoff()
    {
        int failures;
        lock (_lock)
            failures = _failures;
        var steps = Math.Min(failures + 1, 5);
        var delay = TimeSpan.FromTicks(PollInterval.Ticks * (1L << steps));
        return delay > MaxBackoff ? MaxBackoff : delay;
    }

    private TimeSpan? ReadRetryAfter(ApiException ex)
    {
        if (ex.Headers is HttpResponseHeaders http && http.RetryAfter is { } retry)
        {
            if (retry.Delta is { } delta && delta > TimeSpan.Zero)
                return delta;
            if (retry.Date is { } date)
            {
                var wait = date - _timeProvider.GetUtcNow();
                if (wait > TimeSpan.Zero)
                    return wait;
            }
        }

        if (ex.Headers is not null
            && ex.Headers.TryGetValues("Retry-After", out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                && seconds > 0)
                return TimeSpan.FromSeconds(seconds);
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
            {
                var wait = when - _timeProvider.GetUtcNow();
                if (wait > TimeSpan.Zero)
                    return wait;
            }
        }

        return null;
    }

    private static string FormatApiError(ApiException ex)
    {
        var code = (int)ex.StatusCode;
        var reason = ex.ReasonPhrase?.Trim();
        return string.IsNullOrEmpty(reason)
            ? $"GitHub returned {code}."
            : $"GitHub returned {code} ({reason}).";
    }

    private static async Task<Notification[]> ListAllNotificationsAsync(
        IGitHubReposApi api,
        PagedGitHubSession session,
        CancellationToken token)
    {
        session.Reset();
        var items = new List<Notification>();
        for (var page = 0; page < MaxNotificationPages; page++)
        {
            if (page > 0 && (!session.HasNextPage || !session.Advance()))
                break;

            session.PrepareRequest();
            using var pageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            pageCts.CancelAfter(TimeSpan.FromSeconds(30));
            var response = await api.ListNotifications().FirstAsync(pageCts.Token);
            ThrowIfFailed(response);
            items.AddRange(response.Content ?? []);
            session.ApplyLink(response.Headers);
        }

        return [.. items];
    }

    private static void ThrowIfFailed<T>(ApiResponse<T> response)
    {
        if (response.HasRequestError(out var requestError))
            throw requestError;
        if (response.HasResponseError(out var apiError))
            throw apiError;
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)(response.StatusCode ?? 0);
            throw new HttpRequestException(
                $"Response status code does not indicate success: {code}.",
                inner: null,
                statusCode: response.StatusCode);
        }
    }

    private void SetLastError(string? error)
    {
        if (string.Equals(_lastError, error, StringComparison.Ordinal))
            return;

        _lastError = error;
        LastErrorChanged?.Invoke(error);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        Stop();
        _cts.Dispose();
    }
}
