using Xunit;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// Gate for optional live GitHub API tests.
/// Set <see cref="EnvVarName"/> to a classic or fine-grained PAT for a disposable
/// test account. Never commit tokens; load from a local secret store or CI secret.
/// </summary>
internal static class LiveGitHubTestGate
{
    public const string EnvVarName = "GITPULSE_TEST_PAT";

    public static string? Token
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(EnvVarName);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public static void SkipIfUnavailable()
    {
        Assert.SkipWhen(
            Token is null,
            $"Set {EnvVarName} to run live GitHub integration tests.");
    }
}
