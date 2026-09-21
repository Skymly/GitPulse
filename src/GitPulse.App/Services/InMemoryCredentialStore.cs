using GitPulse.Core.Abstractions;

namespace GitPulse.App.Services;

/// <summary>
/// Process-lifetime PAT store. UI Test Host uses this so smoke tests do not
/// write the daily platform store (Windows token.bin / Android SecureStorage).
/// </summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly object _gate = new();
    private string? _token;

    public Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_token);
    }

    public Task SetTokenAsync(string token, CancellationToken ct = default)
    {
        lock (_gate)
            _token = token;
        return Task.CompletedTask;
    }

    public Task ClearTokenAsync(CancellationToken ct = default)
    {
        lock (_gate)
            _token = null;
        return Task.CompletedTask;
    }
}
