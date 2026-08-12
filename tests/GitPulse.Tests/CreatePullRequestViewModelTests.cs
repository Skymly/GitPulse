using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class CreatePullRequestViewModelTests
{
    private static string PrJson(int number, string title) =>
        $"{{\"number\":{number},\"title\":\"{title}\",\"state\":\"open\"," +
        $"\"body\":\"\",\"user\":{{\"login\":\"alice\"}}}}";

    private static void SetValidInputs(CreatePullRequestViewModel vm, string title = "My new PR")
    {
        vm.TitleInput.Value = title;
        vm.HeadInput.Value = "feature";
        vm.BaseInput.Value = "main";
    }

    [Fact]
    public void Initialize_SetsOwnerAndRepo()
    {
        var vm = new CreatePullRequestViewModel(new FakeGitHubClientFactory(new MockHttpHandler()));
        vm.Initialize("Skymly", "GitPulse");
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithEmptyTitle_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "   ";
        vm.HeadInput.Value = "feature";
        vm.BaseInput.Value = "main";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Null(vm.CreatedPullRequestNumber.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithMissingHead_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "Title";
        vm.HeadInput.Value = "   ";
        vm.BaseInput.Value = "main";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Null(vm.CreatedPullRequestNumber.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithMissingBase_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "Title";
        vm.HeadInput.Value = "feature";
        vm.BaseInput.Value = "";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Null(vm.CreatedPullRequestNumber.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithSameHeadAndBase_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "Title";
        vm.HeadInput.Value = "main";
        vm.BaseInput.Value = "main";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Null(vm.CreatedPullRequestNumber.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithValidInputs_SetsCreatedPullRequestNumber()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse(PrJson(99, "My new PR"));
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        SetValidInputs(vm);
        vm.BodyInput.Value = "This is the body";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(99, vm.CreatedPullRequestNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithDraft_SendsDraftTrueInRequest()
    {
        string? capturedBody = null;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", req =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    var bodyTask = req.Content?.ReadAsStringAsync();
                    capturedBody = bodyTask?.Result ?? "";
                    return new MockResponse(PrJson(55, "Draft PR"));
                }

                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        SetValidInputs(vm, "Draft PR");
        vm.IsDraft.Value = true;
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotNull(capturedBody);
        Assert.Contains("\"draft\":true", capturedBody);
        Assert.Equal(55, vm.CreatedPullRequestNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        SetValidInputs(vm, "Test");
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Null(vm.CreatedPullRequestNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithNotFoundResponse_SetsErrorMessage()
    {
        var handler = new MockHttpHandler(); // No routes → 404
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        SetValidInputs(vm, "Test");
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Contains("Create failed", vm.ErrorMessage.Value);
        Assert.Null(vm.CreatedPullRequestNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WhileSaving_DoesNothing()
    {
        var postCount = 0;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", req =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    postCount++;
                    return new MockResponse(PrJson(99, "Should not create"));
                }

                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        SetValidInputs(vm);
        vm.IsSaving.Value = true;
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal(0, postCount);
        Assert.Null(vm.CreatedPullRequestNumber.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }
}
