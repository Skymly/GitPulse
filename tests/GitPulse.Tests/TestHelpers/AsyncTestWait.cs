using Xunit;

namespace GitPulse.Tests.TestHelpers;

/// <summary>Polling helpers for async ViewModel side effects in unit tests.</summary>
internal static class AsyncTestWait
{
    public static async Task UntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }

        Assert.Fail($"Condition not met within {timeoutMs}ms.");
    }
}
