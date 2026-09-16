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
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch (Exception)
        {
            // Keystore / EncryptedSharedPreferences can throw when the device
            // has no lock screen or the blob cannot be decrypted. Match
            // Windows Get: treat as no token, not a crash.
            return null;
        }
    }

    public async Task SetTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not store the token.", ex);
        }
    }

    public Task ClearTokenAsync(CancellationToken ct = default)
    {
        try
        {
            SecureStorage.Default.Remove(TokenKey);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(new InvalidOperationException("Could not clear the token.", ex));
        }
    }
}
