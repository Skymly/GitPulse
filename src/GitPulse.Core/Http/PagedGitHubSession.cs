using System.Net.Http.Headers;

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
    private const int DefaultPerPage = 30;

    private readonly GitHubQueryHandler _queryHandler;
    private int _currentPage = 1;
    private bool _disposed;

    /// <summary>
    /// Creates a session over an authenticated (or unauthenticated) client whose
    /// handler pipeline already includes <paramref name="queryHandler"/>.
    /// </summary>
    public PagedGitHubSession(HttpClient client, GitHubQueryHandler queryHandler)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(queryHandler);
        Client = client;
        _queryHandler = queryHandler;
    }

    /// <summary>HttpClient for <c>RestService.For</c>.</summary>
    public HttpClient Client { get; }

    /// <summary>Whether a <c>rel="next"</c> Link was present on the last response.</summary>
    public bool HasNextPage { get; private set; }

    /// <summary>
    /// Optional issues/PRs state filter (<c>open</c>, <c>closed</c>, <c>all</c>).
    /// Written to the query handler on <see cref="PrepareRequest"/>.
    /// </summary>
    public string? State { get; set; }

    /// <summary>Resets the page cursor to 1 and clears <see cref="HasNextPage"/>. Preserves <see cref="State"/>.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
        _currentPage = 1;
        HasNextPage = false;
    }

    /// <summary>
    /// Copies the current page cursor, default per_page, and <see cref="State"/>
    /// onto the query handler before a RestAPI call.
    /// </summary>
    public void PrepareRequest()
    {
        ThrowIfDisposed();
        _queryHandler.Page = _currentPage;
        _queryHandler.PerPage = DefaultPerPage;
        _queryHandler.State = State;
    }

    /// <summary>Updates <see cref="HasNextPage"/> from response <c>Link</c> headers.</summary>
    public void ApplyLink(HttpResponseHeaders? headers)
    {
        ThrowIfDisposed();
        HasNextPage = LinkHeaderParser.GetNextUrl(headers) is not null;
    }

    /// <summary>
    /// Advances the page cursor for LoadMore. Returns <c>false</c> (no-op) when
    /// <see cref="HasNextPage"/> is false.
    /// </summary>
    public bool Advance()
    {
        ThrowIfDisposed();
        if (!HasNextPage)
            return false;

        _currentPage++;
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
