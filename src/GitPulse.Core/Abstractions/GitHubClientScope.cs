namespace GitPulse.Core.Abstractions;

/// <summary>
/// A short-lived GitHub <see cref="HttpClient"/>.
/// Dispose the scope to dispose the client. The factory keeps the inner handler.
/// </summary>
public sealed class GitHubClientScope(HttpClient client) : IDisposable
{
    public HttpClient Client { get; } = client ?? throw new ArgumentNullException(nameof(client));

    public void Dispose() => Client.Dispose();
}
