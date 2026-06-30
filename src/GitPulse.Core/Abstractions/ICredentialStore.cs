namespace GitPulse.Core.Abstractions;

/// <summary>
/// Platform-agnostic credential storage abstraction.
/// Implementations: Windows (DPAPI), Android (SecureStorage).
/// </summary>
public interface ICredentialStore
{
    Task<string?> GetTokenAsync(CancellationToken ct = default);

    Task SetTokenAsync(string token, CancellationToken ct = default);

    Task ClearTokenAsync(CancellationToken ct = default);
}
