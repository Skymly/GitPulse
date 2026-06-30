using System.Net.Http.Headers;

namespace GitPulse.Core.Abstractions;

/// <summary>
/// Creates an <see cref="HttpClient"/> pre-configured with the GitHub PAT
/// and required headers (Authorization, Accept, User-Agent).
/// </summary>
public interface IGitHubClientFactory
{
    Task<HttpClient> CreateClientAsync(CancellationToken ct = default);
}
