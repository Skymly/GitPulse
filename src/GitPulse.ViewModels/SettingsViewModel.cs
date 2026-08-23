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
    private readonly IGitHubClientFactory _clientFactory;

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

    public SettingsViewModel(ICredentialStore credentialStore, IGitHubClientFactory clientFactory)
    {
        _credentialStore = credentialStore;
        _clientFactory = clientFactory;
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
            var login = await ProbeLoginAsync(token);
            if (string.IsNullOrEmpty(login))
                return;

            await _credentialStore.SetTokenAsync(token);
            TokenInput.Value = string.Empty;
            HasToken.Value = true;
            ViewerLogin.Value = login;
            StatusMessage.Value = $"Token saved. Signed in as {login}.";
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

    private async Task TryLoadViewerAsync(string token)
    {
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
        using var client = await _clientFactory.CreateClientAsync();
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
            if (current is HttpRequestException http
                && http.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return true;

            if (current.Message.Contains("401", StringComparison.Ordinal)
                || current.Message.Contains("403", StringComparison.Ordinal)
                || current.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
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
