using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;

namespace GitPulse.ViewModels;

/// <summary>
/// Leftover list envelope around <see cref="PagedGitHubSession"/>:
/// token check, PrepareRequest, 30s timeout, ApplyLink, CanLoadMore.
/// List ViewModels map domain items and filters only.
/// A generation counter drops stale Load / Load more responses when a newer
/// request has already replaced the session. Credential invalidation disposes
/// the held session so Load more cannot reuse an old Bearer.
/// </summary>
internal sealed class PagedListCycle : IDisposable
{
    private const int TimeoutSeconds = 30;
    private readonly IGitHubClientFactory _factory;
    private readonly Action? _onInvalidated;
    private readonly Action _drop;
    private PagedGitHubSession? _session;
    private int _generation;
    private CancellationTokenSource _abort = new();
    private CancellationTokenSource? _runCts;

    public PagedListCycle(IGitHubClientFactory factory, Action? onInvalidated = null)
    {
        _factory = factory;
        _onInvalidated = onInvalidated;
        _drop = OnCredentialsInvalidated;
        CredentialEpoch.For(factory).Invalidated += _drop;
    }

    public bool HasSession => _session is not null;

    public bool HasNextPage => _session?.HasNextPage == true;

    public bool CanLoadMore => HasSession && HasNextPage;

    public HttpClient? Client => _session?.Client;

    public async Task<PagedListCycleResult<T>> LoadAsync<T>(
        string? state,
        Func<HttpClient, CancellationToken, Task<PagedListPage<T>>> fetch)
    {
        var generation = BeginRun(out var token);

        _session?.Dispose();
        _session = null;

        PagedGitHubSession session;
        try
        {
            session = await _factory.CreatePagedSessionAsync(token);
        }
        catch (OperationCanceledException)
        {
            return StaleOrTimeout<T>(generation);
        }

        if (!IsCurrent(generation))
        {
            session.Dispose();
            return PagedListCycleResult<T>.Noop;
        }

        if (session.Client.DefaultRequestHeaders.Authorization is null)
        {
            session.Dispose();
            return PagedListCycleResult<T>.Unauthenticated;
        }

        _session = session;
        _session.State = state;
        _session.Reset();
        _session.PrepareRequest();
        return await RunAsync(fetch, loadMore: false, generation, token);
    }

    public async Task<PagedListCycleResult<T>> LoadMoreAsync<T>(
        Func<HttpClient, CancellationToken, Task<PagedListPage<T>>> fetch)
    {
        var generation = Volatile.Read(ref _generation);
        if (_session is null || !_session.HasNextPage || !_session.Advance())
            return PagedListCycleResult<T>.Noop;

        _session.PrepareRequest();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        return await RunAsync(fetch, loadMore: true, generation, timeout.Token);
    }

    public void Dispose()
    {
        CredentialEpoch.For(_factory).Invalidated -= _drop;
        DropSession();
        _abort.Dispose();
    }

    private void OnCredentialsInvalidated()
    {
        DropSession();
        _onInvalidated?.Invoke();
    }

    private void DropSession()
    {
        _abort.Cancel();
        _abort.Dispose();
        _abort = new CancellationTokenSource();
        _runCts?.Dispose();
        _runCts = null;
        _session?.Dispose();
        _session = null;
        Interlocked.Increment(ref _generation);
    }

    private int BeginRun(out CancellationToken token)
    {
        _abort.Cancel();
        _abort.Dispose();
        _abort = new CancellationTokenSource();
        _runCts?.Dispose();
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
        _runCts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        token = _runCts.Token;
        return Interlocked.Increment(ref _generation);
    }

    private bool IsCurrent(int generation) => Volatile.Read(ref _generation) == generation;

    private async Task<PagedListCycleResult<T>> RunAsync<T>(
        Func<HttpClient, CancellationToken, Task<PagedListPage<T>>> fetch,
        bool loadMore,
        int generation,
        CancellationToken token)
    {
        try
        {
            var page = await fetch(_session!.Client, token);
            if (!IsCurrent(generation))
                return PagedListCycleResult<T>.Noop;

            _session.ApplyCopiedLink(page.LinkHeader);
            return PagedListCycleResult<T>.Ok(page.Items, _session.HasNextPage);
        }
        catch (OperationCanceledException)
        {
            return StaleOrTimeout<T>(generation);
        }
        catch (Exception ex)
        {
            if (!IsCurrent(generation))
                return PagedListCycleResult<T>.Noop;

            var message = loadMore
                ? $"Load more failed: {ex.Message}"
                : $"Load failed: {ex.Message}";
            return PagedListCycleResult<T>.Fail(message, _session!.HasNextPage);
        }
    }

    private PagedListCycleResult<T> StaleOrTimeout<T>(int generation)
    {
        if (!IsCurrent(generation))
            return PagedListCycleResult<T>.Noop;

        return PagedListCycleResult<T>.Fail("Request timed out.", _session?.HasNextPage == true);
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
