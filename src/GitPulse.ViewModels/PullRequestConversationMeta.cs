using System.Collections.ObjectModel;
using System.Net;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Conversation metadata: assignees, labels, and pending review requests.
/// </summary>
internal sealed class PullRequestConversationMeta(
    PullRequestConversationIo io,
    BindableReactiveProperty<PullRequest?> pullRequest,
    BindableReactiveProperty<bool> isSaving) : IDisposable
{
    public ObservableCollection<User> Assignees { get; } = [];

    public ObservableCollection<Label> Labels { get; } = [];

    public ObservableCollection<User> RequestedReviewers { get; } = [];

    public ObservableCollection<Team> RequestedTeams { get; } = [];

    public BindableReactiveProperty<string> ReviewerLogin { get; } = new(string.Empty);

    public BindableReactiveProperty<string> AssigneeLogin { get; } = new(string.Empty);

    public BindableReactiveProperty<string> LabelInput { get; } = new(string.Empty);

    public BindableReactiveProperty<bool> CanManageReviewers { get; } = new(false);

    public BindableReactiveProperty<bool> HasRequestedReviewers { get; } = new(false);

    public void Sync(PullRequest pr)
    {
        CanManageReviewers.Value = pr.State == "open" && !pr.Merged;
        ApplyAssignees(pr.Assignees);
        ApplyLabels(pr.Labels);
    }

    public async Task LoadRequestedAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        try
        {
            var requested = await api.ListRequestedReviewers(io.Owner, io.Repo, io.Number)
                .FirstAsync(cancellationToken);
            ReplaceRequested(requested);
        }
        catch
        {
            RequestedReviewers.Clear();
            RequestedTeams.Clear();
            HasRequestedReviewers.Value = false;
        }
    }

    public async Task RequestReviewerAsync()
    {
        var login = ReviewerLogin.Value.Trim().TrimStart('@');
        if (login.Length == 0 || pullRequest.Value is null || isSaving.Value || !CanManageReviewers.Value)
            return;

        if (RequestedReviewers.Any(user =>
                string.Equals(user.Login, login, StringComparison.OrdinalIgnoreCase)))
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
                var request = new ReviewersRequest { Reviewers = [login] };
                var response = await api.RequestReviewers(io.Owner, io.Repo, io.Number, request)
                    .FirstAsync(cts.Token);
                if (!ApplyReviewerWriteStatus(response.StatusCode, requesting: true))
                    return;

                ReviewerLogin.Value = string.Empty;
                await LoadRequestedAsync(api, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Request reviewer failed: {ex.Message}";
        }
        finally
        {
            isSaving.Value = false;
        }
    }

    public async Task RemoveRequestedReviewerAsync(string? login)
    {
        login = login?.Trim();
        if (string.IsNullOrEmpty(login) || pullRequest.Value is null || isSaving.Value || !CanManageReviewers.Value)
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
                var request = new ReviewersRequest { Reviewers = [login] };
                var response = await api.RemoveRequestedReviewers(io.Owner, io.Repo, io.Number, request)
                    .FirstAsync(cts.Token);
                if (!ApplyReviewerWriteStatus(response.StatusCode, requesting: false))
                    return;

                await LoadRequestedAsync(api, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Remove reviewer failed: {ex.Message}";
        }
        finally
        {
            isSaving.Value = false;
        }
    }

    public async Task AddAssigneeAsync()
    {
        var login = AssigneeLogin.Value.Trim().TrimStart('@');
        if (login.Length == 0 || pullRequest.Value is null || isSaving.Value || !CanManageReviewers.Value)
            return;

        if (Assignees.Any(user =>
                string.Equals(user.Login, login, StringComparison.OrdinalIgnoreCase)))
            return;

        await WriteAssigneesAsync(
            requesting: true,
            api => api.AddIssueAssignees(
                io.Owner, io.Repo, io.Number, new AssigneesRequest { Assignees = [login] }));
    }

    public async Task RemoveAssigneeAsync(string? login)
    {
        login = login?.Trim();
        if (string.IsNullOrEmpty(login) || pullRequest.Value is null || isSaving.Value || !CanManageReviewers.Value)
            return;

        await WriteAssigneesAsync(
            requesting: false,
            api => api.RemoveIssueAssignees(
                io.Owner, io.Repo, io.Number, new AssigneesRequest { Assignees = [login] }));
    }

    public async Task SaveLabelsAsync()
    {
        if (pullRequest.Value is null || isSaving.Value || !CanManageReviewers.Value)
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
                var names = LabelInput.Value
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToArray();
                var updated = await api.ReplaceIssueLabels(io.Owner, io.Repo, io.Number, new LabelsReplaceRequest { Labels = names })
                    .FirstAsync(cts.Token);
                ApplyLabels(updated);
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Labels save failed: {ex.Message}";
        }
        finally
        {
            isSaving.Value = false;
        }
    }

    public void Dispose()
    {
        ReviewerLogin.Dispose();
        AssigneeLogin.Dispose();
        LabelInput.Dispose();
        CanManageReviewers.Dispose();
        HasRequestedReviewers.Dispose();
    }

    private async Task WriteAssigneesAsync(
        bool requesting,
        Func<IGitHubReposApi, R3.Observable<Observables.RestAPI.ApiResponse<Issue>>> call)
    {
        isSaving.Value = true;
        io.Error.Value = string.Empty;

        try
        {
            var (api, cts) = await io.OpenAsync();
            if (api is null || cts is null)
                return;

            using (cts)
            {
                var response = await call(api).FirstAsync(cts.Token);
                var code = (int)(response.StatusCode ?? 0);
                if (code is < 200 or >= 300)
                {
                    io.Error.Value = code switch
                    {
                        403 => "Not allowed to change assignees.",
                        422 => requesting
                            ? "GitHub rejected that assignee login."
                            : "GitHub could not remove that assignee.",
                        _ => $"Assignee update failed: {code}.",
                    };
                    return;
                }

                if (requesting)
                    AssigneeLogin.Value = string.Empty;
                ApplyAssignees(response.Content?.Assignees);
            }
        }
        catch (OperationCanceledException)
        {
            io.Timeout();
        }
        catch (Exception ex)
        {
            io.Error.Value = $"Assignee update failed: {ex.Message}";
        }
        finally
        {
            isSaving.Value = false;
        }
    }

    private bool ApplyReviewerWriteStatus(HttpStatusCode? status, bool requesting)
    {
        if (status is null || ((int)status >= 200 && (int)status < 300))
            return true;

        io.Error.Value = status switch
        {
            HttpStatusCode.UnprocessableEntity =>
                "GitHub rejected that reviewer. They may not be a collaborator.",
            HttpStatusCode.Forbidden => "Not allowed to change review requests.",
            _ => requesting
                ? $"Request reviewer failed: {(int)status}."
                : $"Remove reviewer failed: {(int)status}.",
        };
        return false;
    }

    private void ReplaceRequested(RequestedReviewers requested)
    {
        RequestedReviewers.Clear();
        foreach (var user in requested.Users ?? [])
            RequestedReviewers.Add(user);

        RequestedTeams.Clear();
        foreach (var team in requested.Teams ?? [])
            RequestedTeams.Add(team);

        HasRequestedReviewers.Value = RequestedReviewers.Count > 0 || RequestedTeams.Count > 0;
    }

    private void ApplyAssignees(User[]? assignees)
    {
        Assignees.Clear();
        foreach (var user in assignees ?? [])
            Assignees.Add(user);
    }

    private void ApplyLabels(Label[]? labels)
    {
        Labels.Clear();
        foreach (var label in labels ?? [])
            Labels.Add(label);
        LabelInput.Value = string.Join(", ", Labels.Select(l => l.Name));
    }
}
