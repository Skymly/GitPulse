using System.Net;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class WorkflowRunDetailViewModelTests
{
    private const string RunJson = """
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
        }
        """;

    private const string JobsJson = """
        {
          "total_count": 1,
          "jobs": [
            {
              "id": 501,
              "run_id": 101,
              "name": "build",
              "status": "completed",
              "conclusion": "success",
              "html_url": "https://github.com/o/r/actions/runs/101/job/501",
              "started_at": "2026-07-01T00:00:10Z",
              "completed_at": "2026-07-01T00:00:50Z"
            }
          ]
        }
        """;

    [Fact]
    public async Task Load_WithoutToken_SetsError()
    {
        using var vm = new WorkflowRunDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler(), token: null),
            new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 101);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Null(vm.Run.Value);
    }

    [Fact]
    public async Task Load_PopulatesRunAndJobs()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/actions/runs/101/jobs", JobsJson)
            .When("/repos/owner/repo/actions/runs/101", RunJson);
        using var vm = new WorkflowRunDetailViewModel(
            new FakeGitHubClientFactory(handler),
            new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 101);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.Run.Value);
        Assert.Equal(101, vm.Run.Value!.Id);
        Assert.Contains("#12", vm.Title.Value);
        Assert.Contains("success", vm.StatusSummary.Value);
        Assert.Single(vm.Jobs);
        Assert.Equal("build", vm.Jobs[0].Name);
    }

    [Fact]
    public async Task Rerun_WhenAllowed_ReloadsWithoutError()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/actions/runs/101/jobs", JobsJson)
            .When("/repos/owner/repo/actions/runs/101", RunJson)
            .When(
                "/repos/owner/repo/actions/runs/101/rerun",
                _ => new MockResponse("{}", StatusCode: HttpStatusCode.Created));
        using var vm = new WorkflowRunDetailViewModel(
            new FakeGitHubClientFactory(handler),
            new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 101);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RerunCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.Run.Value);
    }

    [Fact]
    public async Task OpenInBrowser_DelegatesToLauncher()
    {
        var launcher = new FakeBrowserLauncher();
        using var vm = new WorkflowRunDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()),
            launcher);

        await vm.OpenInBrowserCommand.ExecuteAsync("https://example.com/run");

        Assert.Equal(["https://example.com/run"], launcher.OpenedUrls);
    }
}
