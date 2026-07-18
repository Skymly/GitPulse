using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class WorkflowRunsViewModelTests
{
    private const string RunsJson = """
        {
          "total_count": 2,
          "workflow_runs": [
            {
              "id": 101,
              "name": "CI",
              "display_title": "fix: build",
              "head_branch": "main",
              "head_sha": "abc",
              "run_number": 12,
              "event": "push",
              "status": "completed",
              "conclusion": "success",
              "workflow_id": 1,
              "html_url": "https://github.com/o/r/actions/runs/101",
              "created_at": "2026-07-01T00:00:00Z",
              "updated_at": "2026-07-01T00:01:00Z"
            },
            {
              "id": 102,
              "name": "CI",
              "display_title": "feat: ui",
              "head_branch": "main",
              "head_sha": "def",
              "run_number": 13,
              "event": "push",
              "status": "in_progress",
              "conclusion": null,
              "workflow_id": 1,
              "html_url": "https://github.com/o/r/actions/runs/102",
              "created_at": "2026-07-01T01:00:00Z",
              "updated_at": "2026-07-01T01:01:00Z"
            }
          ]
        }
        """;

    private const string LinkHasNext =
        "<https://api.github.com/repos/owner/repo/actions/runs?page=2>; rel=\"next\"";

    [Fact]
    public void Initialize_SetsOwnerRepoAndFullName()
    {
        using var vm = new WorkflowRunsViewModel(new FakeGitHubClientFactory(new MockHttpHandler()));
        vm.Initialize("Skymly", "GitPulse");

        Assert.Equal("Skymly", vm.Owner.Value);
        Assert.Equal("GitPulse", vm.RepoName.Value);
        Assert.Equal("Skymly/GitPulse", vm.RepoFullName.Value);
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        using var vm = new WorkflowRunsViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler(), token: null));
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(vm.Runs);
    }

    [Fact]
    public async Task Load_WithToken_PopulatesRunsAndCanLoadMore()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/actions/runs", RunsJson, LinkHasNext);
        using var vm = new WorkflowRunsViewModel(new FakeGitHubClientFactory(handler));
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(2, vm.Runs.Count);
        Assert.Equal(101, vm.Runs[0].Id);
        Assert.True(vm.CanLoadMore.Value);
    }

    [Fact]
    public async Task LoadMore_AppendsNextPage()
    {
        var page2 = """
            {
              "total_count": 3,
              "workflow_runs": [
                {
                  "id": 103,
                  "name": "CI",
                  "display_title": "page 2",
                  "head_branch": "main",
                  "head_sha": "ghi",
                  "run_number": 14,
                  "event": "push",
                  "status": "completed",
                  "conclusion": "failure",
                  "workflow_id": 1,
                  "html_url": "https://github.com/o/r/actions/runs/103",
                  "created_at": "2026-07-01T02:00:00Z",
                  "updated_at": "2026-07-01T02:01:00Z"
                }
              ]
            }
            """;

        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/actions/runs", RunsJson, LinkHasNext);
        using var vm = new WorkflowRunsViewModel(new FakeGitHubClientFactory(handler));
        vm.Initialize("owner", "repo");
        await vm.LoadCommand.ExecuteAsync(null);

        handler.When("/repos/owner/repo/actions/runs", page2);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(3, vm.Runs.Count);
        Assert.Equal(103, vm.Runs[2].Id);
        Assert.False(vm.CanLoadMore.Value);
    }
}
