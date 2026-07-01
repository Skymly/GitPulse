using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class CreateIssueViewModelTests
{
    private static string IssueJson(int number, string title) =>
        $"{{\"number\":{number},\"title\":\"{title}\",\"state\":\"open\"," +
        $"\"body\":\"\",\"user\":{{\"login\":\"alice\"}}}}";

    [Fact]
    public void Initialize_SetsOwnerAndRepo()
    {
        var vm = new CreateIssueViewModel(new FakeGitHubClientFactory(new MockHttpHandler()));
        vm.Initialize("Skymly", "GitPulse");
        // No public Owner/Repo properties to assert, but Create should work.
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithEmptyTitle_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "   ";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Null(vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithValidTitle_SetsCreatedIssueNumber()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse(IssueJson(99, "My new issue"));
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "My new issue";
        vm.BodyInput.Value = "This is the body";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(99, vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "Test";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Null(vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }
}
