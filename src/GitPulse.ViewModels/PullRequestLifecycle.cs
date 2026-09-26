using System.Net;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Conversation lifecycle: mergeability, merge, update branch, and open/closed toggle.
/// </summary>
internal sealed class PullRequestLifecycle(
    PullRequestConversationIo io,
    BindableReactiveProperty<PullRequest?> pullRequest,
    BindableReactiveProperty<bool> isSaving,
    Action<PullRequest> apply) : IDisposable
{
    public BindableReactiveProperty<string> MergeMethod { get; } = new("merge");

    public BindableReactiveProperty<bool> CanMerge { get; } = new(false);

    public BindableReactiveProperty<string> MergeStatus { get; } = new(string.Empty);

    public BindableReactiveProperty<bool> IsMerged { get; } = new(false);

    public BindableReactiveProperty<bool> CanUpdateBranch { get; } = new(false);

    public BindableReactiveProperty<bool> IsUpdatingBranch { get; } = new(false);

    public void Sync(PullRequest pr)
    {
        CanUpdateBranch.Value = pr.State == "open" && !pr.Merged;
        SyncMergeStatus(pr);
    }

    public void SyncMergeStatus(PullRequest pr)
    {
        IsMerged.Value = pr.Merged;

        if (pr.Merged)
        {
            CanMerge.Value = false;
            MergeStatus.Value = "Merged";
            return;
        }

        if (pr.State != "open")
        {
            CanMerge.Value = false;
            MergeStatus.Value = "Closed";
            return;
        }

        if (pr.Draft)
        {
            CanMerge.Value = false;
            MergeStatus.Value = "Draft — needs to be marked ready for review";
            return;
        }

        CanMerge.Value = pr.Mergeable ?? false;
        MergeStatus.Value = pr.Mergeable switch
        {
            true => pr.MergeableState == "clean" ? "Mergeable" : $"Mergeable ({pr.MergeableState})",
            false => "Conflicts — cannot merge",
            null => "Checking mergeability...",
        };
    }

    public async Task ToggleStateAsync()
    {
        if (pullRequest.Value is null || isSaving.Value)
            return;

        isSaving.Value = true;
        io.Error.Value = string.Empty;

        try
        {
            var (scope, api, cts) = await io.OpenAsync();
            if (scope is null || api is null || cts is null)
                return;

            using (scope)
            using (cts)
            {
                var newState = pullRequest.Value.State == "open" ? "closed" : "open";
                try
                {
                    var request = new IssueUpdateRequest { State = newState };
                    await api.UpdateIssue(io.Owner, io.Repo, io.Number, request).FirstAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    io.Timeout();
                    return;
                }
                catch (Exception ex)
                {
                    io.Error.Value = $"State change failed: {ex.Message}";
                    return;
                }

                await RefreshAfterWriteAsync(api, cts.Token, pr => Copy(pr, state: newState));
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"State change failed: {ex.Message}";
        }
        finally
        {
            isSaving.Value = false;
        }
    }

    public async Task MergeAsync()
    {
        if (pullRequest.Value is null || isSaving.Value || !CanMerge.Value)
            return;

        isSaving.Value = true;
        io.Error.Value = string.Empty;

        try
        {
            var (scope, api, cts) = await io.OpenAsync();
            if (scope is null || api is null || cts is null)
                return;

            using (scope)
            using (cts)
            {
                MergeResponse response;
                try
                {
                    var headSha = pullRequest.Value.Head?.Sha;
                    var request = new MergeRequest
                    {
                        Method = MergeMethod.Value,
                        CommitTitle = $"Merge #{pullRequest.Value.Number} {pullRequest.Value.Title}",
                        Sha = string.IsNullOrEmpty(headSha) ? null : headSha,
                    };

                    response = await api.MergePullRequest(io.Owner, io.Repo, io.Number, request)
                        .FirstAsync(cts.Token);
                }
                catch (Exception ex) when (WriteTimeout.IsCanceled(ex))
                {
                    await ConfirmMergeAfterTimeoutAsync(api);
                    return;
                }
                catch (ApiException ex) when ((int)ex.StatusCode == 409)
                {
                    io.Error.Value = "The pull request branch has changed. Refresh and try again.";
                    return;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    io.Error.Value = "The pull request branch has changed. Refresh and try again.";
                    return;
                }
                catch (Exception ex)
                {
                    io.Error.Value = $"Merge failed: {ex.Message}";
                    return;
                }

                if (!response.Merged)
                {
                    io.Error.Value = response.Message;
                    return;
                }

                await RefreshAfterWriteAsync(
                    api, cts.Token, pr => Copy(pr, state: "closed", merged: true));
            }
        }
        catch (Exception ex) when (WriteTimeout.IsCanceled(ex))
        {
            io.Error.Value = WriteTimeout.MaybeSubmitted;
        }
        catch (ApiException ex) when ((int)ex.StatusCode == 409)
        {
            io.Error.Value = "The pull request branch has changed. Refresh and try again.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            io.Error.Value = "The pull request branch has changed. Refresh and try again.";
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Merge failed: {ex.Message}";
        }
        finally
        {
            isSaving.Value = false;
        }
    }

    public async Task UpdateBranchAsync()
    {
        if (pullRequest.Value is null || isSaving.Value || IsUpdatingBranch.Value || !CanUpdateBranch.Value)
            return;

        isSaving.Value = true;
        IsUpdatingBranch.Value = true;
        io.Error.Value = string.Empty;

        try
        {
            var (scope, api, cts) = await io.OpenAsync();
            if (scope is null || api is null || cts is null)
                return;

            using (scope)
            using (cts)
            {
                var headSha = pullRequest.Value.Head?.Sha;
                var request = new UpdatePullRequestBranchRequest
                {
                    ExpectedHeadSha = string.IsNullOrEmpty(headSha) ? null : headSha,
                };
                using var response = await api.UpdatePullRequestBranch(io.Owner, io.Repo, io.Number, request)
                    .FirstAsync(cts.Token);
                var code = (int)(response.StatusCode ?? 0);
                if (code is >= 200 and < 300)
                {
                    await RefreshAfterWriteAsync(api, cts.Token, fallback: null);
                    return;
                }

                io.Error.Value = code switch
                {
                    403 => "Not allowed to update this pull request branch.",
                    422 => "GitHub could not update this pull request branch.",
                    _ => $"Update branch failed: {code}.",
                };
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Update branch failed: {ex.Message}";
        }
        finally
        {
            IsUpdatingBranch.Value = false;
            isSaving.Value = false;
        }
    }

    private async Task RefreshAfterWriteAsync(
        IGitHubReposApi api,
        CancellationToken cancellationToken,
        Func<PullRequest, PullRequest>? fallback)
    {
        try
        {
            var pr = await api.GetPullRequest(io.Owner, io.Repo, io.Number)
                .FirstAsync(cancellationToken);
            apply(pr);
        }
        catch (Exception)
        {
            if (fallback is not null && pullRequest.Value is { } current)
                apply(fallback(current));
            io.Error.Value = "The change was saved. Refresh to see the latest state.";
        }
    }

    private async Task ConfirmMergeAfterTimeoutAsync(IGitHubReposApi api)
    {
        try
        {
            using var confirm = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var pr = await api.GetPullRequest(io.Owner, io.Repo, io.Number)
                .FirstAsync(confirm.Token);
            if (pr.Merged)
            {
                apply(pr);
                return;
            }
        }
        catch (Exception)
        {
            // Still unknown whether GitHub accepted the merge.
        }

        io.Error.Value = WriteTimeout.MaybeSubmitted;
    }

    private static PullRequest Copy(
        PullRequest pr,
        string? state = null,
        bool? merged = null)
    {
        return new()
        {
            Number = pr.Number,
            Title = pr.Title,
            Body = pr.Body,
            State = state ?? pr.State,
            Draft = pr.Draft,
            Merged = merged ?? pr.Merged,
            HtmlUrl = pr.HtmlUrl,
            CreatedAt = pr.CreatedAt,
            UpdatedAt = pr.UpdatedAt,
            User = pr.User,
            MergedBy = pr.MergedBy,
            Assignees = pr.Assignees,
            Labels = pr.Labels,
            Head = pr.Head,
            Base = pr.Base,
            Mergeable = merged is true ? false : pr.Mergeable,
            MergeableState = pr.MergeableState,
            MergeCommitSha = pr.MergeCommitSha,
            Commits = pr.Commits,
            Additions = pr.Additions,
            Deletions = pr.Deletions,
            ChangedFiles = pr.ChangedFiles,
        };
    }

    public void Dispose()
    {
        MergeMethod.Dispose();
        CanMerge.Dispose();
        MergeStatus.Dispose();
        IsMerged.Dispose();
        CanUpdateBranch.Dispose();
        IsUpdatingBranch.Dispose();
    }
}
