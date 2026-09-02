using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Conversation lifecycle: mergeability, merge, update branch, ready for review,
/// convert to draft, and open/closed toggle.
/// </summary>
internal sealed class PullRequestLifecycle(
    PullRequestConversationIo io,
    BindableReactiveProperty<PullRequest?> pullRequest,
    BindableReactiveProperty<bool> isSaving,
    Action<PullRequest> apply,
    Action<PullRequest> syncReviewPermissions) : IDisposable
{
    public BindableReactiveProperty<string> MergeMethod { get; } = new("merge");

    public BindableReactiveProperty<bool> CanMerge { get; } = new(false);

    public BindableReactiveProperty<string> MergeStatus { get; } = new(string.Empty);

    public BindableReactiveProperty<bool> IsMerged { get; } = new(false);

    public BindableReactiveProperty<bool> CanUpdateBranch { get; } = new(false);

    public BindableReactiveProperty<bool> IsUpdatingBranch { get; } = new(false);

    public BindableReactiveProperty<bool> CanMarkReadyForReview { get; } = new(false);

    public BindableReactiveProperty<bool> IsMarkingReadyForReview { get; } = new(false);

    public BindableReactiveProperty<bool> CanConvertToDraft { get; } = new(false);

    public BindableReactiveProperty<bool> IsConvertingToDraft { get; } = new(false);

    public void Sync(PullRequest pr)
    {
        CanUpdateBranch.Value = pr.State == "open" && !pr.Merged;
        CanMarkReadyForReview.Value = pr.State == "open" && pr.Draft && !pr.Merged;
        CanConvertToDraft.Value = pr.State == "open" && !pr.Draft && !pr.Merged;
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
            var (api, cts) = await io.OpenAsync(requireToken: false);
            if (api is null || cts is null)
                return;

            using (cts)
            {
                var newState = pullRequest.Value.State == "open" ? "closed" : "open";
                var request = new IssueUpdateRequest { State = newState };
                await api.UpdateIssue(io.Owner, io.Repo, io.Number, request).FirstAsync(cts.Token);

                var pr = pullRequest.Value;
                pullRequest.Value = new PullRequest
                {
                    Number = pr.Number,
                    Title = pr.Title,
                    Body = pr.Body,
                    State = newState,
                    Draft = pr.Draft,
                    Merged = pr.Merged,
                    HtmlUrl = pr.HtmlUrl,
                    CreatedAt = pr.CreatedAt,
                    UpdatedAt = pr.UpdatedAt,
                    User = pr.User,
                    MergedBy = pr.MergedBy,
                    HeadRef = pr.HeadRef,
                    BaseRef = pr.BaseRef,
                };
                syncReviewPermissions(pullRequest.Value);
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
            var (api, cts) = await io.OpenAsync();
            if (api is null || cts is null)
                return;

            using (cts)
            {
                var request = new MergeRequest
                {
                    Method = MergeMethod.Value,
                    CommitTitle = $"Merge #{pullRequest.Value.Number} {pullRequest.Value.Title}",
                };

                var response = await api.MergePullRequest(io.Owner, io.Repo, io.Number, request)
                    .FirstAsync(cts.Token);

                if (response.Merged)
                {
                    var pr = pullRequest.Value;
                    pullRequest.Value = new PullRequest
                    {
                        Number = pr.Number,
                        Title = pr.Title,
                        Body = pr.Body,
                        State = "closed",
                        Draft = pr.Draft,
                        Merged = true,
                        HtmlUrl = pr.HtmlUrl,
                        CreatedAt = pr.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        User = pr.User,
                        MergedBy = pr.User,
                        HeadRef = pr.HeadRef,
                        BaseRef = pr.BaseRef,
                        Mergeable = false,
                        MergeableState = pr.MergeableState,
                        MergeCommitSha = response.Sha,
                        Commits = pr.Commits,
                        Additions = pr.Additions,
                        Deletions = pr.Deletions,
                        ChangedFiles = pr.ChangedFiles,
                    };
                    SyncMergeStatus(pullRequest.Value);
                    syncReviewPermissions(pullRequest.Value);
                }
                else
                {
                    io.Error.Value = response.Message;
                }
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
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
        if (pullRequest.Value is null || IsUpdatingBranch.Value || !CanUpdateBranch.Value)
            return;

        IsUpdatingBranch.Value = true;
        io.Error.Value = string.Empty;

        try
        {
            var (api, cts) = await io.OpenAsync();
            if (api is null || cts is null)
                return;

            using (cts)
            {
                var headSha = pullRequest.Value.Head?.Sha;
                var request = new UpdatePullRequestBranchRequest
                {
                    ExpectedHeadSha = string.IsNullOrEmpty(headSha) ? null : headSha,
                };
                var response = await api.UpdatePullRequestBranch(io.Owner, io.Repo, io.Number, request)
                    .FirstAsync(cts.Token);
                var code = (int)(response.StatusCode ?? 0);
                if (code is >= 200 and < 300)
                {
                    var pr = await api.GetPullRequest(io.Owner, io.Repo, io.Number).FirstAsync(cts.Token);
                    apply(pr);
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
        }
    }

    public async Task MarkReadyForReviewAsync()
    {
        if (pullRequest.Value is null || IsMarkingReadyForReview.Value || !CanMarkReadyForReview.Value)
            return;

        IsMarkingReadyForReview.Value = true;
        io.Error.Value = string.Empty;

        try
        {
            var (api, cts) = await io.OpenAsync();
            if (api is null || cts is null)
                return;

            using (cts)
            {
                var response = await api.MarkPullRequestReadyForReview(io.Owner, io.Repo, io.Number)
                    .FirstAsync(cts.Token);
                var code = (int)(response.StatusCode ?? 0);
                if (code is >= 200 and < 300)
                {
                    var pr = await api.GetPullRequest(io.Owner, io.Repo, io.Number).FirstAsync(cts.Token);
                    apply(pr);
                    return;
                }

                io.Error.Value = code switch
                {
                    403 => "Not allowed to mark this pull request ready for review.",
                    422 => "GitHub could not mark this pull request ready for review.",
                    _ => $"Ready for review failed: {code}.",
                };
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Ready for review failed: {ex.Message}";
        }
        finally
        {
            IsMarkingReadyForReview.Value = false;
        }
    }

    public async Task ConvertToDraftAsync()
    {
        if (pullRequest.Value is null || IsConvertingToDraft.Value || !CanConvertToDraft.Value)
            return;

        IsConvertingToDraft.Value = true;
        io.Error.Value = string.Empty;

        try
        {
            var (api, cts) = await io.OpenAsync();
            if (api is null || cts is null)
                return;

            using (cts)
            {
                var response = await api.ConvertPullRequestToDraft(io.Owner, io.Repo, io.Number)
                    .FirstAsync(cts.Token);
                var code = (int)(response.StatusCode ?? 0);
                if (code is >= 200 and < 300)
                {
                    var pr = await api.GetPullRequest(io.Owner, io.Repo, io.Number).FirstAsync(cts.Token);
                    apply(pr);
                    return;
                }

                io.Error.Value = code switch
                {
                    403 => "Not allowed to convert this pull request to draft.",
                    422 => "GitHub could not convert this pull request to draft.",
                    _ => $"Convert to draft failed: {code}.",
                };
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Convert to draft failed: {ex.Message}";
        }
        finally
        {
            IsConvertingToDraft.Value = false;
        }
    }

    public void Dispose()
    {
        MergeMethod.Dispose();
        CanMerge.Dispose();
        MergeStatus.Dispose();
        IsMerged.Dispose();
        CanUpdateBranch.Dispose();
        IsUpdatingBranch.Dispose();
        CanMarkReadyForReview.Dispose();
        IsMarkingReadyForReview.Dispose();
        CanConvertToDraft.Dispose();
        IsConvertingToDraft.Dispose();
    }
}
