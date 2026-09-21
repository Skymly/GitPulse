using GitPulse.Core.Abstractions;

namespace GitPulse.App.Services;

/// <summary>
/// Routes PAT storage to <see cref="InMemoryCredentialStore"/> while UI Test
/// Host is on, otherwise to the daily platform store.
/// </summary>
/// <remarks>
/// The flag is re-read on every call. Android sets GITPULSE_UI_TEST_HOST from
/// the launch intent in MainActivity.OnCreate, after this singleton has been
/// registered (and possibly after the first resolve).
/// </remarks>
public sealed class UiTestHostCredentialStore : ICredentialStore
{
    private readonly ICredentialStore _daily;
    private readonly InMemoryCredentialStore _memory = new();

    public UiTestHostCredentialStore(ICredentialStore daily)
    {
        ArgumentNullException.ThrowIfNull(daily);
        _daily = daily;
    }

    public Task<string?> GetTokenAsync(CancellationToken ct = default)
        => Active.GetTokenAsync(ct);

    public Task SetTokenAsync(string token, CancellationToken ct = default)
        => Active.SetTokenAsync(token, ct);

    public Task ClearTokenAsync(CancellationToken ct = default)
        => Active.ClearTokenAsync(ct);

    private ICredentialStore Active => UiTestHost.IsEnabled ? _memory : _daily;
}
