using System.Net.Http.Headers;
using GitPulse.Core.Http;

namespace GitPulse.Core.Abstractions;

/// <summary>
/// Creates GitHub-authenticated HTTP clients.
/// </summary>
/// <remarks>
/// The factory owns the inner <see cref="HttpMessageHandler"/> for the process
/// lifetime. Returned <see cref="HttpClient"/> instances must be constructed
/// with <c>disposeHandler: false</c> so disposing a client does not dispose
/// the shared handler. Callers dispose the client; prefer <see cref="OpenAsync"/>.
/// </remarks>
public interface IGitHubClientFactory
{
    /// <summary>
    /// Creates a basic client (no pagination handler).
    /// The caller should dispose the client. The inner handler stays with the factory.
    /// Prefer <see cref="OpenAsync"/> when the call site can use <c>using</c>.
    /// </summary>
    Task<HttpClient> CreateClientAsync(CancellationToken ct = default);

    /// <summary>
    /// Opens a disposable client scope. Disposing the scope disposes the
    /// <see cref="HttpClient"/>, not the factory-owned handler.
    /// </summary>
    async Task<GitHubClientScope> OpenAsync(CancellationToken ct = default)
    {
        var client = await CreateClientAsync(ct).ConfigureAwait(false);
        return new GitHubClientScope(client);
    }

    /// <summary>
    /// Creates a client backed by a <see cref="GitHubQueryHandler"/>
    /// for pagination/filtering. The handler is returned so the caller can
    /// set <c>Page</c>/<c>PerPage</c>/<c>State</c> before each request.
    /// Prefer <see cref="CreatePagedSessionAsync"/> for list pagination.
    /// The inner handler under the query handler stays with the factory.
    /// </summary>
    Task<(HttpClient Client, GitHubQueryHandler QueryHandler)> CreatePagedClientAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Creates a <see cref="PagedGitHubSession"/> that owns the paged client
    /// cycle (page cursor, Link <c>HasNextPage</c>, query-handler injection, dispose).
    /// Disposing the session disposes the client, not the factory-owned inner handler.
    /// </summary>
    Task<PagedGitHubSession> CreatePagedSessionAsync(CancellationToken ct = default);
}
