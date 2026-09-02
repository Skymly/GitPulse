using System.Collections.ObjectModel;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Conversation review composer: submitted reviews, viewer permissions, and submit.
/// </summary>
internal sealed class PullRequestReviewComposer(
    PullRequestConversationIo io,
    BindableReactiveProperty<PullRequest?> pullRequest,
    BindableReactiveProperty<bool> isSaving,
    Action<PullRequest> apply,
    Func<IGitHubReposApi, CancellationToken, Task> reloadRequestedReviewers) : IDisposable
{
    public ObservableCollection<PullRequestReview> Reviews { get; } = [];

    public BindableReactiveProperty<string> ReviewEvent { get; } = new("COMMENT");

    public BindableReactiveProperty<string> ReviewBody { get; } = new(string.Empty);

    public BindableReactiveProperty<string> ViewerLogin { get; } = new(string.Empty);

    public BindableReactiveProperty<bool> CanReview { get; } = new(false);

    public BindableReactiveProperty<bool> CanApproveOrRequestChanges { get; } = new(true);

    public ObservableCollection<string> ReviewEventOptions { get; } = ["COMMENT", "APPROVE", "REQUEST_CHANGES"];

    public void SyncPermissions(PullRequest pr)
    {
        CanReview.Value = pr.State == "open" && !pr.Merged;
        var author = pr.User?.Login;
        CanApproveOrRequestChanges.Value = string.IsNullOrEmpty(ViewerLogin.Value)
            || string.IsNullOrEmpty(author)
            || !string.Equals(ViewerLogin.Value, author, StringComparison.OrdinalIgnoreCase);

        ReviewEventOptions.Clear();
        ReviewEventOptions.Add("COMMENT");
        if (CanApproveOrRequestChanges.Value)
        {
            ReviewEventOptions.Add("APPROVE");
            ReviewEventOptions.Add("REQUEST_CHANGES");
        }

        if (!ReviewEventOptions.Any(option =>
                string.Equals(option, ReviewEvent.Value, StringComparison.OrdinalIgnoreCase)))
            ReviewEvent.Value = "COMMENT";
    }

    public async Task LoadAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        try
        {
            var user = await api.GetAuthenticatedUser().FirstAsync(cancellationToken);
            ViewerLogin.Value = user.Login ?? string.Empty;
        }
        catch
        {
            ViewerLogin.Value = string.Empty;
        }

        if (pullRequest.Value is not null)
            SyncPermissions(pullRequest.Value);

        try
        {
            var reviews = await api.ListPullRequestReviews(io.Owner, io.Repo, io.Number)
                .FirstAsync(cancellationToken);
            Replace(reviews);
        }
        catch
        {
            Reviews.Clear();
        }
    }

    public void Replace(IEnumerable<PullRequestReview> reviews)
    {
        Reviews.Clear();
        foreach (var review in reviews)
        {
            if (string.Equals(review.State, "PENDING", StringComparison.OrdinalIgnoreCase))
                continue;
            Reviews.Add(review);
        }
    }

    public async Task SubmitAsync()
    {
        if (pullRequest.Value is null || isSaving.Value || !CanReview.Value)
            return;

        var reviewEvent = ReviewEvent.Value;
        if (string.IsNullOrWhiteSpace(reviewEvent))
            return;

        var isApprove = string.Equals(reviewEvent, "APPROVE", StringComparison.OrdinalIgnoreCase);
        var isRequestChanges = string.Equals(reviewEvent, "REQUEST_CHANGES", StringComparison.OrdinalIgnoreCase);
        if ((isApprove || isRequestChanges) && !CanApproveOrRequestChanges.Value)
            return;

        if (!isApprove && string.IsNullOrWhiteSpace(ReviewBody.Value))
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
                var request = new PullRequestReviewCreateRequest
                {
                    Event = reviewEvent,
                    Body = string.IsNullOrWhiteSpace(ReviewBody.Value) ? null : ReviewBody.Value,
                    CommitId = pullRequest.Value.Head?.Sha,
                };
                await api.CreatePullRequestReview(io.Owner, io.Repo, io.Number, request)
                    .FirstAsync(cts.Token);

                ReviewBody.Value = string.Empty;

                try
                {
                    var reviews = await api.ListPullRequestReviews(io.Owner, io.Repo, io.Number)
                        .FirstAsync(cts.Token);
                    Replace(reviews);
                }
                catch
                {
                    // Keep the local list; submit already succeeded.
                }

                await reloadRequestedReviewers(api, cts.Token);

                try
                {
                    var pr = await api.GetPullRequest(io.Owner, io.Repo, io.Number).FirstAsync(cts.Token);
                    apply(pr);
                }
                catch
                {
                    // Submit succeeded; mergeable refresh is best-effort.
                }
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Review failed: {ex.Message}";
        }
        finally
        {
            isSaving.Value = false;
        }
    }

    public void Dispose()
    {
        ReviewEvent.Dispose();
        ReviewBody.Dispose();
        ViewerLogin.Dispose();
        CanReview.Dispose();
        CanApproveOrRequestChanges.Dispose();
    }
}
