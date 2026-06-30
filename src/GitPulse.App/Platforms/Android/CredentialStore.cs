using GitPulse.Core.Abstractions;
using Microsoft.Maui.Storage;

namespace GitPulse.App.Platforms.Android;

/// <summary>
/// Android credential store using MAUI SecureStorage (backed by
/// Android KeyStore with EncryptedSharedPreferences).
/// </summary>
public sealed class AndroidCredentialStore : ICredentialStore
{
    private const string TokenKey = "github_pat";

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        return await SecureStorage.Default.GetAsync(TokenKey);
    }

    public async Task SetTokenAsync(string token, CancellationToken ct = default)
    {
        await SecureStorage.Default.SetAsync(TokenKey, token);
    }

    public Task ClearTokenAsync(CancellationToken ct = default)
    {
        SecureStorage.Default.Remove(TokenKey);
        return Task.CompletedTask;
    }
}
