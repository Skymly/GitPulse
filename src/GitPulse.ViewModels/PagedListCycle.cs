using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;

namespace GitPulse.ViewModels;

/// <summary>
/// Owns the current list session and its page cursor. A refresh or credential change
/// detaches the old session and cancels its requests. An in-flight fetch keeps its
/// client until the HTTP operation returns, including when a caller times out.
/// </summary>
internal sealed class PagedListCycle : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private readonly IGitHubClientFactory _factory;
    private readonly Action? _onInvalidated;
    private readonly IDisposable _credentials;
    private readonly object _gate = new();
    private Generation? _current;
    private bool _disposed;

    public PagedListCycle(IGitHubClientFactory factory, Action? onInvalidated = null)
    {
        _factory = factory;
        _onInvalidated = onInvalidated;
        _credentials = CredentialEpoch.Subscribe(factory, OnCredentialsInvalidated);
    }

    public bool HasSession
    {
        get { lock (_gate) return _current?.Session is not null; }
    }

    public bool CanLoadMore
    {
        get { lock (_gate) return _current?.Session?.HasNextPage == true; }
    }

    public async Task<PagedListCycleResult<T>> LoadAsync<T>(
        string? state,
        Func<HttpClient, Task<PagedListPage<T>>> fetch)
    {
        Generation next;
        Generation? previous;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            previous = _current;
            _current = next = new Generation();
        }
        try
        {
            previous?.Retire();
            using var timeout = next.CreateTimeout();
            var session = await _factory.CreatePagedSessionAsync(timeout.Token);
            if (!TryAttach(next, session, state, timeout.Token))
            {
                if (Volatile.Read(ref _current) != next)
                    return PagedListCycleResult<T>.Noop;

                timeout.Token.ThrowIfCancellationRequested();
                return PagedListCycleResult<T>.Unauthenticated;
            }

            return await FetchPageAsync(next, fetch, loadMore: false, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return TimeoutOrStale<T>(next);
        }
        finally
        {
            next.Release();
        }
    }

    private bool TryAttach(Generation generation, PagedGitHubSession session, string? state, CancellationToken token)
    {
        try
        {
            lock (_gate)
            {
                if (_current != generation || token.IsCancellationRequested ||
                    session.Client.DefaultRequestHeaders.Authorization is null)
                    return false;

                // HttpClient enforces this per send, so a page timing out cannot cancel a sibling.
                if (session.Client.Timeout == Timeout.InfiniteTimeSpan || session.Client.Timeout > RequestTimeout)
                    session.Client.Timeout = RequestTimeout;
                session.State = state;
                session.Reset();
                generation.Session = session;
                return true;
            }
        }
        finally
        {
            if (generation.Session != session)
                session.Dispose();
        }
    }

    public async Task<PagedListCycleResult<T>> LoadMoreAsync<T>(Func<HttpClient, Task<PagedListPage<T>>> fetch)
    {
        var current = BorrowSession(advance: true);
        if (current is null)
            return PagedListCycleResult<T>.Noop;

        try
        {
            using var timeout = current.CreateTimeout();
            return await FetchPageAsync(current, fetch, loadMore: true, timeout.Token);
        }
        finally
        {
            current.Release();
        }
    }

    public async Task UseClientAsync(Func<Generation, CancellationToken, Task> use)
    {
        var current = BorrowSession(advance: false);
        if (current is null)
            return;

        try
        {
            using var timeout = current.CreateTimeout();
            await use(current, timeout.Token);
        }
        finally
        {
            current.Release();
        }
    }

    private Generation? BorrowSession(bool advance)
    {
        lock (_gate)
        {
            var current = _current;
            if (current?.Session is not { } session || (advance && !session.Advance()))
                return null;

            current.Retain();
            return current;
        }
    }

    private async Task<PagedListCycleResult<T>> FetchPageAsync<T>(
        Generation current, Func<HttpClient, Task<PagedListPage<T>>> fetch, bool loadMore, CancellationToken token)
    {
        var session = current.Session!;
        try
        {
            // Keep the subscription alive until the generated send actually finishes.
            var page = await current.SendAsync(client => session.PrepareRequest(() => fetch(client)), token);
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_current != current)
                    return PagedListCycleResult<T>.Noop;

                session.ApplyCopiedLink(page.LinkHeader);
                return PagedListCycleResult<T>.Ok(page.Items, session.HasNextPage);
            }
        }
        catch (OperationCanceledException)
        {
            return TimeoutOrStale<T>(current);
        }
        catch (Exception ex)
        {
            if (Volatile.Read(ref _current) != current)
                return PagedListCycleResult<T>.Noop;

            var message = loadMore ? $"Load more failed: {ex.Message}" : $"Load failed: {ex.Message}";
            return PagedListCycleResult<T>.Fail(message, session.HasNextPage);
        }
    }

    private PagedListCycleResult<T> TimeoutOrStale<T>(Generation generation)
        => Volatile.Read(ref _current) == generation
            ? PagedListCycleResult<T>.Fail("Request timed out.", generation.Session?.HasNextPage == true)
            : PagedListCycleResult<T>.Noop;

    public void Dispose()
    {
        _credentials.Dispose();
        DropSession(dispose: true);
    }

    private void OnCredentialsInvalidated()
    {
        DropSession(dispose: false);
        _onInvalidated?.Invoke();
    }

    private void DropSession(bool dispose)
    {
        Generation? previous;
        lock (_gate)
        {
            _disposed |= dispose;
            previous = _current;
            _current = null;
        }
        previous?.Retire();
    }

    internal sealed class Generation
    {
        private readonly CancellationTokenSource _stop = new();
        // Initially owned by the cycle and the first LoadAsync, including factory acquisition.
        private int _owners = 2;
        public PagedGitHubSession? Session { get; set; }
        public bool IsRetired => _stop.IsCancellationRequested;

        public CancellationTokenSource CreateTimeout()
        {
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(RequestTimeout);
            return timeout;
        }

        public void Retain() => Interlocked.Increment(ref _owners);

        public void Release()
        {
            if (Interlocked.Decrement(ref _owners) == 0)
            {
                Session?.Dispose();
                _stop.Dispose();
            }
        }

        public void Retire()
        {
            try
            {
                _stop.Cancel();
                Session?.Client.CancelPendingRequests();
            }
            finally
            {
                Release();
            }
        }

        public async Task<T> SendAsync<T>(Func<HttpClient, Task<T>> send, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var client = Session!.Client;
            // The generated, bodyless GETs subscribe synchronously and enter HttpClient
            // before returning this task. Keep this contract when adding a caller.
            var request = send(client);
            // CancelPendingRequests covers sends already started. Close the race if
            // retirement occurred between the token check and entering HttpClient.
            if (IsRetired)
                client.CancelPendingRequests();

            try
            {
                return await request.WaitAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // The waiter can leave immediately. Transfer its ownership to the
                // actual send until that task (including response handling) unwinds.
                Retain();
                _ = ReleaseAfterRequestAsync(request);
                throw;
            }
        }

        private async Task ReleaseAfterRequestAsync(Task request)
        {
            try
            {
                await request.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The caller already observed cancellation. Consume a late transport failure.
            }
            finally
            {
                Release();
            }
        }
    }
}

internal readonly record struct PagedListPage<T>(T[] Items, string? LinkHeader);

internal readonly record struct PagedListCycleResult<T>(
    string? Error,
    T[] Items,
    bool HasNextPage,
    bool Authenticated,
    bool Completed)
{
    public static PagedListCycleResult<T> Unauthenticated { get; } = new(
        "No token configured. Open Settings to add a GitHub PAT.",
        [],
        false,
        false,
        true);

    public static PagedListCycleResult<T> Noop { get; } = new(null, [], false, true, false);

    public static PagedListCycleResult<T> Ok(T[] items, bool hasNextPage) =>
        new(null, items, hasNextPage, true, true);

    public static PagedListCycleResult<T> Fail(string error, bool hasNextPage) =>
        new(error, [], hasNextPage, true, true);
}
