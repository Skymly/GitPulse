using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Latest Check Runs plus Commit Statuses on a SHA, summarized as
/// pending / success / failure / no checks.
/// </summary>
internal static class HeadGateRollup
{
    public const string NoChecks = "No checks";
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string Failure = "Failure";

    public static async Task<HeadGateRollupState> LoadAsync(
        IGitHubReposApi api,
        string owner,
        string repo,
        string? sha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sha))
            return HeadGateRollupState.Empty;

        CheckRun[] runs = [];
        CombinedCommitStatus? combined = null;

        try
        {
            var result = await api.ListCheckRunsForRef(owner, repo, sha, "latest")
                .FirstAsync(cancellationToken);
            runs = result.CheckRuns ?? [];
        }
        catch
        {
            runs = [];
        }

        try
        {
            combined = await api.GetCombinedStatusForRef(owner, repo, sha)
                .FirstAsync(cancellationToken);
        }
        catch
        {
            combined = null;
        }

        return new HeadGateRollupState(
            Compute(runs, combined),
            runs,
            combined?.Statuses ?? []);
    }

    public static string Compute(
        IReadOnlyList<CheckRun> runs,
        CombinedCommitStatus? combined)
    {
        var statuses = combined?.Statuses ?? [];
        if (runs.Count == 0 && statuses.Length == 0)
            return NoChecks;

        if (runs.Any(IsIncompleteCheckRun) ||
            statuses.Any(status => status.State.Equals("pending", StringComparison.OrdinalIgnoreCase)))
            return Pending;

        if (runs.Any(IsFailedCheckRun) ||
            statuses.Any(status =>
                status.State.Equals("failure", StringComparison.OrdinalIgnoreCase) ||
                status.State.Equals("error", StringComparison.OrdinalIgnoreCase)))
            return Failure;

        return Success;
    }

    private static bool IsIncompleteCheckRun(CheckRun run) =>
        !run.Status.Equals("completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailedCheckRun(CheckRun run)
    {
        var conclusion = run.Conclusion;
        return conclusion is not null &&
               (conclusion.Equals("failure", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("timed_out", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("cancelled", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("startup_failure", StringComparison.OrdinalIgnoreCase) ||
                conclusion.Equals("action_required", StringComparison.OrdinalIgnoreCase));
    }
}

internal readonly record struct HeadGateRollupState(
    string Summary,
    CheckRun[] Runs,
    CommitStatus[] Statuses)
{
    public static HeadGateRollupState Empty { get; } = new(HeadGateRollup.NoChecks, [], []);
}
