using System.Net;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Latest Check Runs plus Commit Statuses on a SHA, summarized as
/// pending / success / failure / no checks / error / partial.
/// HTTP 404 on a Gate endpoint is "no data from that source". Any other
/// failure is <see cref="Error"/> with a reason for Inline Error; the
/// pull request or commit still loads. When <c>total_count</c> exceeds the
/// first page, Success / No checks become <see cref="Partial"/> so a hidden
/// later-page failure cannot look green.
/// </summary>
internal static class HeadGateRollup
{
    public const string NoChecks = "No checks";
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string Failure = "Failure";
    public const string Error = "Error";
    public const string Partial = "Partial";

    public static async Task<HeadGateRollupState> LoadAsync(
        IGitHubReposApi api,
        string owner,
        string repo,
        string? sha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sha))
            return HeadGateRollupState.Empty;

        var (runs, truncated, runError) = await LoadCheckRunsAsync(api, owner, repo, sha, cancellationToken);
        var (combined, statusError) = await LoadCombinedStatusAsync(api, owner, repo, sha, cancellationToken);
        var statuses = combined?.Statuses ?? [];
        var error = runError ?? statusError;
        if (error is not null)
            return new HeadGateRollupState(Error, runs, statuses, error);

        var summary = Compute(runs, combined);
        if (truncated && (summary == Success || summary == NoChecks))
            summary = Partial;

        return new HeadGateRollupState(summary, runs, statuses);
    }

    private static async Task<(CheckRun[] Runs, bool Truncated, string? Error)> LoadCheckRunsAsync(
        IGitHubReposApi api,
        string owner,
        string repo,
        string sha,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await api.ListCheckRunsForRef(owner, repo, sha, "latest")
                .FirstAsync(cancellationToken);
            var runs = result.CheckRuns ?? [];
            return (runs, result.TotalCount > runs.Length, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            return ([], false, null);
        }
        catch (Exception ex)
        {
            return ([], false, FormatGateError(ex));
        }
    }

    private static async Task<(CombinedCommitStatus? Combined, string? Error)> LoadCombinedStatusAsync(
        IGitHubReposApi api,
        string owner,
        string repo,
        string sha,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await api.GetCombinedStatusForRef(owner, repo, sha)
                .FirstAsync(cancellationToken), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            return (null, null);
        }
        catch (Exception ex)
        {
            return (null, FormatGateError(ex));
        }
    }

    private static bool IsNotFound(Exception ex) =>
        ex is ApiException { StatusCode: HttpStatusCode.NotFound }
        || ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound };

    private static string FormatGateError(Exception ex)
    {
        if (ex is ApiException api)
        {
            var code = (int)api.StatusCode;
            var reason = api.ReasonPhrase?.Trim();
            return string.IsNullOrEmpty(reason)
                ? $"GitHub returned {code}."
                : $"GitHub returned {code} ({reason}).";
        }

        return ex.Message;
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
    CommitStatus[] Statuses,
    string? ErrorMessage = null)
{
    public static HeadGateRollupState Empty { get; } = new(HeadGateRollup.NoChecks, [], []);
}
