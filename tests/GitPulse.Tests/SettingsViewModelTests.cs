using GitPulse.Core.Abstractions;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class SettingsViewModelTests
{
    private sealed class FakeCredentialStore : ICredentialStore
    {
        private string? _token;
        public bool SetTokenCalled { get; private set; }
        public bool ClearTokenCalled { get; private set; }

        public FakeCredentialStore(string? token = null) => _token = token;

        public Task<string?> GetTokenAsync(CancellationToken ct = default)
            => Task.FromResult(_token);

        public Task SetTokenAsync(string token, CancellationToken ct = default)
        {
            _token = token;
            SetTokenCalled = true;
            return Task.CompletedTask;
        }

        public Task ClearTokenAsync(CancellationToken ct = default)
        {
            _token = null;
            ClearTokenCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_WithExistingToken_SetsHasTokenTrue()
    {
        var store = new FakeCredentialStore("ghp_existing");
        var vm = new SettingsViewModel(store);

        // LoadStatusAsync runs in the constructor — give it a tick.
        Assert.True(vm.HasToken.Value);
        vm.Dispose();
    }

    [Fact]
    public void Constructor_WithoutToken_SetsHasTokenFalse()
    {
        var store = new FakeCredentialStore(null);
        var vm = new SettingsViewModel(store);

        Assert.False(vm.HasToken.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_WithValidToken_PersistsAndClearsInput()
    {
        var store = new FakeCredentialStore(null);
        var vm = new SettingsViewModel(store);
        vm.TokenInput.Value = "ghp_new_token";

        await vm.SaveTokenCommand.ExecuteAsync(null);

        Assert.True(store.SetTokenCalled);
        Assert.True(vm.HasToken.Value);
        Assert.Equal(string.Empty, vm.TokenInput.Value);
        Assert.Equal("Token saved.", vm.StatusMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_WithEmptyToken_SetsStatusMessage()
    {
        var store = new FakeCredentialStore(null);
        var vm = new SettingsViewModel(store);

        vm.TokenInput.Value = "";
        await vm.SaveTokenCommand.ExecuteAsync(null);

        Assert.False(store.SetTokenCalled);
        Assert.Equal("Please enter a token first.", vm.StatusMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_WithWhitespaceOnly_SetsStatusMessage()
    {
        var store = new FakeCredentialStore(null);
        var vm = new SettingsViewModel(store);

        vm.TokenInput.Value = "   ";
        await vm.SaveTokenCommand.ExecuteAsync(null);

        Assert.False(store.SetTokenCalled);
        Assert.Equal("Please enter a token first.", vm.StatusMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ClearToken_RemovesTokenAndUpdatesHasToken()
    {
        var store = new FakeCredentialStore("ghp_existing");
        var vm = new SettingsViewModel(store);

        await vm.ClearTokenCommand.ExecuteAsync(null);

        Assert.True(store.ClearTokenCalled);
        Assert.False(vm.HasToken.Value);
        Assert.Equal("Token cleared.", vm.StatusMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_WithStoreException_SetsErrorMessage()
    {
        var store = new ThrowingCredentialStore("Set");
        var vm = new SettingsViewModel(store);
        vm.TokenInput.Value = "ghp_token";

        await vm.SaveTokenCommand.ExecuteAsync(null);

        Assert.Contains("Save failed", vm.StatusMessage.Value);
        Assert.False(vm.IsBusy.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ClearToken_WithStoreException_SetsErrorMessage()
    {
        var store = new ThrowingCredentialStore("Clear");
        var vm = new SettingsViewModel(store);

        await vm.ClearTokenCommand.ExecuteAsync(null);

        Assert.Contains("Clear failed", vm.StatusMessage.Value);
        Assert.False(vm.IsBusy.Value);
        vm.Dispose();
    }

    private sealed class ThrowingCredentialStore : ICredentialStore
    {
        private readonly string _throwOn;

        public ThrowingCredentialStore(string throwOn) => _throwOn = throwOn;

        public Task<string?> GetTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public Task SetTokenAsync(string token, CancellationToken ct = default)
            => _throwOn == "Set" ? throw new InvalidOperationException("Store error") : Task.CompletedTask;

        public Task ClearTokenAsync(CancellationToken ct = default)
            => _throwOn == "Clear" ? throw new InvalidOperationException("Store error") : Task.CompletedTask;
    }
}
