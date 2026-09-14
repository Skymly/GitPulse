using System.Security.Cryptography;
using System.Text;
using GitPulse.Core.Abstractions;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Windows credential store using DPAPI (CurrentUser scope).
/// The token is encrypted and persisted to
/// %LOCALAPPDATA%/GitPulse/token.bin (not roaming).
/// </summary>
public sealed class WindowsCredentialStore : ICredentialStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GitPulse",
        "token.bin");

    private static readonly string LegacyStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitPulse",
        "token.bin");

    public Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        MigrateLegacyStore();
        if (!File.Exists(StorePath))
            return Task.FromResult<string?>(null);

        try
        {
            var cipher = File.ReadAllBytes(StorePath);
            var plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(plain));
        }
        catch (CryptographicException)
        {
            // The data could not be decrypted (e.g. different user profile).
            return Task.FromResult<string?>(null);
        }
        catch (IOException)
        {
            return Task.FromResult<string?>(null);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetTokenAsync(string token, CancellationToken ct = default)
    {
        MigrateLegacyStore();
        var dir = Path.GetDirectoryName(StorePath)!;
        Directory.CreateDirectory(dir);

        var plain = Encoding.UTF8.GetBytes(token);
        var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(StorePath, cipher);

        return Task.CompletedTask;
    }

    public Task ClearTokenAsync(CancellationToken ct = default)
    {
        if (File.Exists(StorePath))
            File.Delete(StorePath);
        if (File.Exists(LegacyStorePath))
            File.Delete(LegacyStorePath);

        return Task.CompletedTask;
    }

    private static void MigrateLegacyStore()
    {
        if (!File.Exists(LegacyStorePath))
            return;

        if (!File.Exists(StorePath))
        {
            var dir = Path.GetDirectoryName(StorePath)!;
            Directory.CreateDirectory(dir);
            File.Copy(LegacyStorePath, StorePath, overwrite: false);
        }

        File.Delete(LegacyStorePath);
    }
}
