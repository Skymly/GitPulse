using System.Net;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Settings page view model — manages GitHub PAT entry, validation, and storage.
/// M23 verifies the token with GET /user before persisting.
/// </summary>
public sealed partial class SettingsViewModel : IDisposable
{
    private readonly ICredentialStore _credentialStore;
    private readonly IGitHubClientFactory? _clientFactory;
    private readonly INotificationPoller? _poller;

    /// <summary>Current PAT input text (two-way bound to Entry).</summary>
    public BindableReactiveProperty<string> TokenInput { get; } = new(string.Empty);

    /// <summary>Whether a token is currently stored.</summary>
    public BindableReactiveProperty<bool> HasToken { get; } = new(false);

    /// <summary>Authenticated login from GET /user; empty when unknown.</summary>
    public BindableReactiveProperty<string> ViewerLogin { get; } = new(string.Empty);

    /// <summary>Status message shown after save/clear.</summary>
    public BindableReactiveProperty<string> StatusMessage { get; } = new(string.Empty);

    /// <summary>Whether an async operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsBusy { get; } = new(false);

    public SettingsViewModel(
        ICredentialStore credentialStore,
        IGitHubClientFactory? clientFactory = null,
        INotificationPoller? poller = null)
    {
        _credentialStore = credentialStore;
        _clientFactory = clientFactory;
        _poller = poller;
        _ = LoadStatusAsync();
    }

    private async Task LoadStatusAsync()
    {
        var token = await _credentialStore.GetTokenAsync();
        HasToken.Value = !string.IsNullOrEmpty(token);
        if (string.IsNullOrEmpty(token))
        {
            ViewerLogin.Value = string.Empty;
            return;
        }

        await TryLoadViewerAsync(token);
    }

    [RelayCommand]
    private async Task SaveTokenAsync()
    {
        var token = TokenInput.Value.Trim();
        if (string.IsNullOrEmpty(token))
        {
            StatusMessage.Value = "Please enter a token first.";
            return;
        }

        IsBusy.Value = true;
        try
        {
            if (_clientFactory is null)
            {
                await _credentialStore.SetTokenAsync(token);
                TokenInput.Value = string.Empty;
                HasToken.Value = true;
                StatusMessage.Value = "Token saved.";
                await ResumePollingAsync();
                return;
            }

            var login = await ProbeLoginAsync(token);
            if (string.IsNullOrEmpty(login))
                return;

            await _credentialStore.SetTokenAsync(token);
            TokenInput.Value = string.Empty;
            HasToken.Value = true;
            ViewerLogin.Value = login;
            StatusMessage.Value = $"Token saved. Signed in as {login}.";
            CredentialEpoch.For(_clientFactory).Invalidate();
            await ResumePollingAsync();
        }
        catch (Exception ex)
        {
            StatusMessage.Value = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy.Value = false;
        }
    }

    [RelayCommand]
    private async Task ClearTokenAsync()
    {
        IsBusy.Value = true;
        try
        {
            await _credentialStore.ClearTokenAsync();
            HasToken.Value = false;
            ViewerLogin.Value = string.Empty;
            StatusMessage.Value = "Token cleared.";
            if (_clientFactory is not null)
                CredentialEpoch.For(_clientFactory).Invalidate();
            _poller?.Stop();
        }
        catch (Exception ex)
        {
            StatusMessage.Value = $"Clear failed: {ex.Message}";
        }
        finally
        {
            IsBusy.Value = false;
        }
    }

    private async Task ResumePollingAsync()
    {
        if (_poller is null)
            return;

        var wasPolling = _poller.IsPolling;
        _poller.Start();
        if (wasPolling)
            await _poller.RefreshAsync();
    }

    private async Task TryLoadViewerAsync(string token)
    {
        if (_clientFactory is null)
            return;

        try
        {
            ViewerLogin.Value = await ProbeLoginAsync(token) ?? string.Empty;
        }
        catch
        {
            ViewerLogin.Value = string.Empty;
        }
    }

    private async Task<string?> ProbeLoginAsync(string token)
    {
        var factory = _clientFactory ?? throw new InvalidOperationException("Client factory required.");
        using var scope = await factory.OpenAsync();
        var client = scope.Client;
        try
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var user = await api.GetAuthenticatedUser().FirstAsync(cts.Token);
            var login = user.Login?.Trim();
            if (string.IsNullOrEmpty(login))
            {
                StatusMessage.Value = "GitHub did not return a login for this token.";
                return null;
            }

            return login;
        }
        catch (Exception ex) when (IsRejectedToken(ex))
        {
            StatusMessage.Value = "GitHub rejected this token.";
            return null;
        }
        catch (OperationCanceledException)
        {
            StatusMessage.Value = "Request timed out.";
            return null;
        }
    }


    private static bool IsRejectedToken(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is ApiException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden })
                return true;

            if (current is HttpRequestException
                {
                    StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                })
                return true;
        }

        return false;
    }
    public void Dispose()
    {
        TokenInput.Dispose();
        HasToken.Dispose();
        ViewerLogin.Dispose();
        StatusMessage.Dispose();
        IsBusy.Dispose();
    }
}
