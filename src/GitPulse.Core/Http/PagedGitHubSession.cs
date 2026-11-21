using System.Net.Http.Headers;
using Observables.RestAPI;

namespace GitPulse.Core.Http;

/// <summary>
/// Owns one paged GitHub HTTP client cycle: <see cref="GitHubQueryHandler"/>
/// page/state injection, current page cursor, Link-header <see cref="HasNextPage"/>,
/// and client dispose. List ViewModels call <c>RestService.For</c> on
/// <see cref="Client"/> and map domain items; they do not reassemble handler +
/// Link parsing.
/// </summary>
public sealed class PagedGitHubSession : IDisposable
{
    private int _currentPage = 1;
    private int? _loadMorePage;
    private bool _disposed;

    /// <summary>
    /// Creates a session over an authenticated (or unauthenticated) client whose
    /// handler pipeline already includes <see cref="GitHubQueryHandler"/>.
    /// </summary>
    public PagedGitHubSession(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        Client = client;
    }

    /// <summary>HttpClient for <c>RestService.For</c>.</summary>
    public HttpClient Client { get; }

    /// <summary>Whether a <c>rel="next"</c> Link was present on the last response.</summary>
    public bool HasNextPage { get; private set; }

    /// <summary>
    /// Optional issues/PRs state filter (<c>open</c>, <c>closed</c>, <c>all</c>).
    /// Captured for each call to <see cref="PrepareRequest{T}"/>.
    /// </summary>
    public string? State { get; set; }

    /// <summary>Resets the page cursor to 1 and clears <see cref="HasNextPage"/>. Preserves <see cref="State"/>.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
        _currentPage = 1;
        _loadMorePage = null;
        HasNextPage = false;
    }

    /// <summary>
    /// Invokes one generated RestAPI call with a snapshot of the current pagination options.
    /// The invocation must create the proxy call synchronously; its observable may be subscribed later.
    /// </summary>
    public T PrepareRequest<T>(Func<T> invoke)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(invoke);
        using var scope = RestApiCallScope.Push(new Dictionary<string, object?>
        {
            [GitHubQueryHandler.PageOption.Key] = _loadMorePage ?? _currentPage,
            [GitHubQueryHandler.PerPageOption.Key] = GitHubQueryHandler.DefaultPerPage,
            [GitHubQueryHandler.StateOption.Key] = State,
        });
        return invoke();
    }

    /// <summary>
    /// Updates <see cref="HasNextPage"/> from response <c>Link</c> headers.
    /// Commits a pending <see cref="Advance"/> so failed LoadMore retries the same page.
    /// </summary>
    public void ApplyLink(HttpResponseHeaders? headers) =>
        ApplyCopiedLink(LinkHeaderParser.CopyLinkHeader(headers));

    /// <summary>
    /// Updates <see cref="HasNextPage"/> from an already-copied <c>Link</c> header.
    /// Use this after <c>ApiResponse&lt;T&gt;</c> has been disposed.
    /// </summary>
    public void ApplyCopiedLink(string? linkHeader)
    {
        ThrowIfDisposed();
        if (_loadMorePage is int page)
        {
            _currentPage = page;
            _loadMorePage = null;
        }

        HasNextPage = LinkHeaderParser.GetNextUrlFromCopy(linkHeader) is not null;
    }

    /// <summary>
    /// Prepares the next page for LoadMore without committing the cursor.
    /// The cursor advances only when <see cref="ApplyLink"/> runs after a successful response.
    /// Returns <c>false</c> (no-op) when <see cref="HasNextPage"/> is false.
    /// </summary>
    public bool Advance()
    {
        ThrowIfDisposed();
        if (!HasNextPage)
            return false;

        _loadMorePage = _currentPage + 1;
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        Client.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
