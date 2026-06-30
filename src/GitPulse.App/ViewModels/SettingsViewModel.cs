using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using R3;

namespace GitPulse.App.ViewModels;

/// <summary>
/// Settings page view model — manages GitHub PAT entry, validation, and storage.
/// State is exposed via R3 <see cref="BindableReactiveProperty{T}"/> for XAML binding.
/// </summary>
public sealed partial class SettingsViewModel : IDisposable
{
    private readonly ICredentialStore _credentialStore;

    /// <summary>Current PAT input text (two-way bound to Entry).</summary>
    public BindableReactiveProperty<string> TokenInput { get; } = new(string.Empty);

    /// <summary>Whether a token is currently stored.</summary>
    public BindableReactiveProperty<bool> HasToken { get; } = new(false);

    /// <summary>Status message shown after save/clear.</summary>
    public BindableReactiveProperty<string> StatusMessage { get; } = new(string.Empty);

    /// <summary>Whether an async operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsBusy { get; } = new(false);

    public SettingsViewModel(ICredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
        _ = LoadStatusAsync();
    }

    private async Task LoadStatusAsync()
    {
        var token = await _credentialStore.GetTokenAsync();
        HasToken.Value = !string.IsNullOrEmpty(token);
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
            await _credentialStore.SetTokenAsync(token);
            TokenInput.Value = string.Empty;
            HasToken.Value = true;
            StatusMessage.Value = "Token saved.";
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

    public void Dispose()
    {
        TokenInput.Dispose();
        HasToken.Dispose();
        StatusMessage.Dispose();
        IsBusy.Dispose();
    }
}
