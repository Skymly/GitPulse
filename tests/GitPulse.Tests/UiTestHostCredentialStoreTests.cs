using GitPulse.App;
using GitPulse.App.Services;
using GitPulse.Core.Abstractions;
using Xunit;

namespace GitPulse.Tests;

public class UiTestHostCredentialStoreTests
{
    private static readonly SemaphoreSlim EnvGate = new(1, 1);

    [Fact]
    public async Task FlagOff_DelegatesToDailyStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await WithHostFlag(null, async () =>
        {
            var daily = new RecordingCredentialStore { Token = "daily" };
            var store = new UiTestHostCredentialStore(daily);

            Assert.Equal("daily", await store.GetTokenAsync(ct));
            await store.SetTokenAsync("written", ct);
            await store.ClearTokenAsync(ct);

            Assert.Equal(1, daily.GetCount);
            Assert.Equal(1, daily.SetCount);
            Assert.Equal(1, daily.ClearCount);
            Assert.Equal("written", daily.LastSet);
            Assert.Null(daily.Token);
        });
    }

    [Fact]
    public async Task FlagOn_UsesMemoryAndDoesNotTouchDaily()
    {
        var ct = TestContext.Current.CancellationToken;
        await WithHostFlag(UiTestHost.EnabledValue, async () =>
        {
            var daily = new RecordingCredentialStore { Token = "developer-pat" };
            var store = new UiTestHostCredentialStore(daily);

            Assert.Null(await store.GetTokenAsync(ct));
            await store.SetTokenAsync("smoke-pat", ct);
            Assert.Equal("smoke-pat", await store.GetTokenAsync(ct));
            await store.ClearTokenAsync(ct);
            Assert.Null(await store.GetTokenAsync(ct));

            Assert.Equal(0, daily.GetCount);
            Assert.Equal(0, daily.SetCount);
            Assert.Equal(0, daily.ClearCount);
            Assert.Equal("developer-pat", daily.Token);
        });
    }

    [Fact]
    public async Task EnableAfterConstruct_SubsequentSetDoesNotCallDaily()
    {
        var ct = TestContext.Current.CancellationToken;
        await WithHostFlag(null, async () =>
        {
            var daily = new RecordingCredentialStore();
            var store = new UiTestHostCredentialStore(daily);

            await store.SetTokenAsync("daily-write", ct);
            Assert.Equal(1, daily.SetCount);

            UiTestHost.Enable();

            await store.SetTokenAsync("memory-write", ct);
            Assert.Equal("memory-write", await store.GetTokenAsync(ct));
            Assert.Equal(1, daily.SetCount);
            Assert.Equal(0, daily.GetCount);
            Assert.Equal("daily-write", daily.Token);
        });
    }

    [Fact]
    public async Task InMemoryStore_RoundTripsAndClears()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCredentialStore();
        Assert.Null(await store.GetTokenAsync(ct));

        await store.SetTokenAsync("pat", ct);
        Assert.Equal("pat", await store.GetTokenAsync(ct));

        await store.ClearTokenAsync(ct);
        Assert.Null(await store.GetTokenAsync(ct));
    }

    [Fact]
    public void Ctor_NullDaily_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UiTestHostCredentialStore(null!));
    }

    static async Task WithHostFlag(string? value, Func<Task> body)
    {
        var ct = TestContext.Current.CancellationToken;
        await EnvGate.WaitAsync(ct);
        string? previous = Environment.GetEnvironmentVariable(UiTestHost.EnvironmentName);
        try
        {
            Environment.SetEnvironmentVariable(UiTestHost.EnvironmentName, value);
            await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(UiTestHost.EnvironmentName, previous);
            EnvGate.Release();
        }
    }

    private sealed class RecordingCredentialStore : ICredentialStore
    {
        public string? Token { get; set; }
        public string? LastSet { get; private set; }
        public int GetCount { get; private set; }
        public int SetCount { get; private set; }
        public int ClearCount { get; private set; }

        public Task<string?> GetTokenAsync(CancellationToken ct = default)
        {
            GetCount++;
            return Task.FromResult(Token);
        }

        public Task SetTokenAsync(string token, CancellationToken ct = default)
        {
            LastSet = token;
            Token = token;
            SetCount++;
            return Task.CompletedTask;
        }

        public Task ClearTokenAsync(CancellationToken ct = default)
        {
            Token = null;
            ClearCount++;
            return Task.CompletedTask;
        }
    }
}
