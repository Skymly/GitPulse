using System.Net;
using GitPulse.Core.Abstractions;
using GitPulse.Tests.TestHelpers;
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

    private static FakeGitHubClientFactory UserFactory(string login = "octocat") =>
        new(new MockHttpHandler().When("/user", $"{{\"login\":\"{login}\"}}"));

    [Fact]
    public void Constructor_WithExistingToken_SetsHasTokenTrue()
    {
        var store = new FakeCredentialStore("ghp_existing");
        var vm = new SettingsViewModel(store, UserFactory());

        Assert.True(vm.HasToken.Value);
        vm.Dispose();
    }

    [Fact]
    public void Constructor_WithoutToken_SetsHasTokenFalse()
    {
        var store = new FakeCredentialStore(null);
        var vm = new SettingsViewModel(store, UserFactory());

        Assert.False(vm.HasToken.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_WithValidToken_PersistsAndShowsLogin()
    {
        var store = new FakeCredentialStore(null);
        var vm = new SettingsViewModel(store, UserFactory("skymly"));
        vm.TokenInput.Value = "ghp_new_token";

        await vm.SaveTokenCommand.ExecuteAsync(null);

        Assert.True(store.SetTokenCalled);
        Assert.True(vm.HasToken.Value);
        Assert.Equal(string.Empty, vm.TokenInput.Value);
        Assert.Equal("skymly", vm.ViewerLogin.Value);
        Assert.Contains("skymly", vm.StatusMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_WithEmptyToken_SetsStatusMessage()
    {
        var store = new FakeCredentialStore(null);
        var vm = new SettingsViewModel(store, UserFactory());

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
        var vm = new SettingsViewModel(store, UserFactory());

        vm.TokenInput.Value = "   ";
        await vm.SaveTokenCommand.ExecuteAsync(null);

        Assert.False(store.SetTokenCalled);
        Assert.Equal("Please enter a token first.", vm.StatusMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_RejectedByGitHub_DoesNotPersist()
    {
        var store = new FakeCredentialStore(null);
        var factory = new FakeGitHubClientFactory(
            new MockHttpHandler().When("/user", _ =>
                new MockResponse("{}", StatusCode: HttpStatusCode.Unauthorized, AttachRequest: true)));
        var vm = new SettingsViewModel(store, factory);
        vm.TokenInput.Value = "ghp_bad";

        await vm.SaveTokenCommand.ExecuteAsync(null);

        Assert.False(store.SetTokenCalled);
        Assert.False(vm.HasToken.Value);
        Assert.Contains("rejected", vm.StatusMessage.Value, StringComparison.OrdinalIgnoreCase);
        vm.Dispose();
    }

    [Fact]
    public async Task ClearToken_RemovesTokenAndUpdatesHasToken()
    {
        var store = new FakeCredentialStore("ghp_existing");
        var vm = new SettingsViewModel(store, UserFactory());

        await vm.ClearTokenCommand.ExecuteAsync(null);

        Assert.True(store.ClearTokenCalled);
        Assert.False(vm.HasToken.Value);
        Assert.Equal(string.Empty, vm.ViewerLogin.Value);
        Assert.Equal("Token cleared.", vm.StatusMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveToken_WithStoreException_SetsErrorMessage()
    {
        var store = new ThrowingCredentialStore("Set");
        var vm = new SettingsViewModel(store, UserFactory());
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
        var vm = new SettingsViewModel(store, UserFactory());

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
