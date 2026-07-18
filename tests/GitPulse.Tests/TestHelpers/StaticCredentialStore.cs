using GitPulse.Core.Abstractions;

namespace GitPulse.Tests.TestHelpers;

internal sealed class StaticCredentialStore(string? token) : ICredentialStore
{
    public Task<string?> GetTokenAsync(CancellationToken ct = default)
        => Task.FromResult(token);

    public Task SetTokenAsync(string token, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task ClearTokenAsync(CancellationToken ct = default)
        => throw new NotSupportedException();
}
