using System.Net.Http.Headers;
using GitPulse.Core.Http;

namespace GitPulse.Core.Abstractions;

/// <summary>
/// Creates an <see cref="HttpClient"/> pre-configured with the GitHub PAT
/// and required headers (Authorization, Accept, User-Agent).
/// </summary>
public interface IGitHubClientFactory
{
    /// <summary>Creates a basic client (no pagination handler).</summary>
    Task<HttpClient> CreateClientAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a client backed by a <see cref="GitHubQueryHandler"/>
    /// for pagination/filtering. The handler is returned so the caller can
    /// set <c>Page</c>/<c>PerPage</c>/<c>State</c> before each request.
    /// Prefer <see cref="CreatePagedSessionAsync"/> for list pagination.
    /// </summary>
    Task<(HttpClient Client, GitHubQueryHandler QueryHandler)> CreatePagedClientAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Creates a <see cref="PagedGitHubSession"/> that owns the paged client
    /// cycle (page cursor, Link <c>HasNextPage</c>, query-handler injection, dispose).
    /// </summary>
    Task<PagedGitHubSession> CreatePagedSessionAsync(CancellationToken ct = default);
}
