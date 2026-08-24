using System.Net.Http.Headers;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;

namespace GitPulse.ViewModels;

/// <summary>
/// Leftover list envelope around <see cref="PagedGitHubSession"/>:
/// token check, PrepareRequest, 30s timeout, ApplyLink, CanLoadMore.
/// List ViewModels map domain items and filters only.
/// </summary>
internal sealed class PagedListCycle(IGitHubClientFactory factory) : IDisposable
{
    private const int TimeoutSeconds = 30;
    private PagedGitHubSession? _session;

    public bool HasSession => _session is not null;

    public bool HasNextPage => _session?.HasNextPage == true;

    public bool CanLoadMore => HasSession && HasNextPage;

    public HttpClient? Client => _session?.Client;

    public async Task<PagedListCycleResult<T>> LoadAsync<T>(
        string? state,
        Func<HttpClient, CancellationToken, Task<PagedListPage<T>>> fetch)
    {
        _session?.Dispose();
        _session = null;

        var session = await factory.CreatePagedSessionAsync();
        if (session.Client.DefaultRequestHeaders.Authorization is null)
        {
            session.Dispose();
            return PagedListCycleResult<T>.Unauthenticated;
        }

        _session = session;
        _session.State = state;
        _session.Reset();
        _session.PrepareRequest();
        return await RunAsync(fetch, loadMore: false);
    }

    public async Task<PagedListCycleResult<T>> LoadMoreAsync<T>(
        Func<HttpClient, CancellationToken, Task<PagedListPage<T>>> fetch)
    {
        if (_session is null || !_session.HasNextPage || !_session.Advance())
            return PagedListCycleResult<T>.Noop;

        _session.PrepareRequest();
        return await RunAsync(fetch, loadMore: true);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }

    private async Task<PagedListCycleResult<T>> RunAsync<T>(
        Func<HttpClient, CancellationToken, Task<PagedListPage<T>>> fetch,
        bool loadMore)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var page = await fetch(_session!.Client, cts.Token);
            _session.ApplyLink(page.Headers);
            return PagedListCycleResult<T>.Ok(page.Items, _session.HasNextPage);
        }
        catch (OperationCanceledException)
        {
            return PagedListCycleResult<T>.Fail("Request timed out.", _session!.HasNextPage);
        }
        catch (Exception ex)
        {
            var message = loadMore
                ? $"Load more failed: {ex.Message}"
                : $"Load failed: {ex.Message}";
            return PagedListCycleResult<T>.Fail(message, _session!.HasNextPage);
        }
    }
}

internal readonly record struct PagedListPage<T>(T[] Items, HttpResponseHeaders? Headers);

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
