using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class CreatePullRequestViewModelTests
{
    private static string PrJson(int number, string title) =>
        GitHubJson.PullRequest(number, title: title, body: "", login: "alice");

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
    public async Task Create_WithEmptyTitle_SetsPageError()
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
        Assert.Equal("Title, head branch, and base branch are required.", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithMissingHead_SetsPageError()
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
        Assert.Equal("Title, head branch, and base branch are required.", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithMissingBase_SetsPageError()
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
        Assert.Equal("Title, head branch, and base branch are required.", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithSameHeadAndBase_SetsPageError()
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
        Assert.Equal("Head and base must be different branches.", vm.ErrorMessage.Value);
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

    private const string BranchesJson =
        "[{\"name\":\"main\",\"commit\":{\"sha\":\"abc123def456\",\"url\":\"https://api.github.com/repos/owner/repo/commits/abc123def456\"},\"protected\":false}," +
        "{\"name\":\"develop\",\"commit\":{\"sha\":\"789012abcdef\",\"url\":\"https://api.github.com/repos/owner/repo/commits/789012abcdef\"},\"protected\":true}]";

    [Fact]
    public async Task LoadBranches_PopulatesBranchesCollection()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/branches", BranchesJson);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadBranchesCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(2, vm.Branches.Count);
        Assert.Equal("main", vm.Branches[0].Name);
        Assert.Equal("develop", vm.Branches[1].Name);
        Assert.True(vm.Branches[1].Protected);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadBranches_FollowsNextLink_ConcatenatesPages()
    {
        const string page1 =
            "[{\"name\":\"main\",\"commit\":{\"sha\":\"abc123def456\",\"url\":\"https://api.github.com/repos/owner/repo/commits/abc123def456\"},\"protected\":false}]";
        const string page2 =
            "[{\"name\":\"feature\",\"commit\":{\"sha\":\"789012abcdef\",\"url\":\"https://api.github.com/repos/owner/repo/commits/789012abcdef\"},\"protected\":false}]";
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/branches", req =>
            {
                var query = req.RequestUri?.Query ?? "";
                if (query.Contains("page=2", StringComparison.Ordinal))
                    return new MockResponse(page2);
                return new MockResponse(
                    page1,
                    "<https://api.github.com/repos/owner/repo/branches?page=2>; rel=\"next\"");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadBranchesCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(["main", "feature"], vm.Branches.Select(b => b.Name).ToArray());
        vm.Dispose();
    }

    [Fact]
    public async Task LoadBranches_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/branches", BranchesJson);
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadBranchesCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(vm.Branches);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadBranches_WithEmptyOwner_DoesNothing()
    {
        var hits = 0;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/branches", _ =>
            {
                hits++;
                return new MockResponse(BranchesJson);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);

        await vm.LoadBranchesCommand.ExecuteAsync(null);

        Assert.Equal(0, hits);
        Assert.Empty(vm.Branches);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadBranches_WhileLoading_DoesNotStartASecondRequest()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hits = 0;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/branches", _ =>
            {
                hits++;
                return new MockResponse(BranchesJson, Gate: gate.Task);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        var first = vm.LoadBranchesCommand.ExecuteAsync(null);
        await AsyncTestWait.UntilAsync(() => vm.IsLoadingBranches.Value && hits > 0);

        await vm.LoadBranchesCommand.ExecuteAsync(null);
        Assert.Equal(1, hits);

        gate.SetResult();
        await first;

        Assert.Equal(2, vm.Branches.Count);
        Assert.Equal(1, hits);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadBranches_WithNotFoundResponse_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadBranchesCommand.ExecuteAsync(null);

        Assert.Contains("Branches load failed", vm.ErrorMessage.Value);
        Assert.Empty(vm.Branches);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WhenTimeoutAndPrExists_SetsCreatedPullRequestNumber()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", req =>
            {
                if (req.Method == HttpMethod.Post)
                    throw new OperationCanceledException();
                return new MockResponse(
                    $"[{GitHubJson.PullRequest(88, title: "My new PR", headRef: "feature")}]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");
        SetValidInputs(vm);

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(88, vm.CreatedPullRequestNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WhenTimeoutAndNoMatch_SetsMaybeSubmitted()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls", req =>
            {
                if (req.Method == HttpMethod.Post)
                    throw new OperationCanceledException();
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreatePullRequestViewModel(factory);
        vm.Initialize("owner", "repo");
        SetValidInputs(vm);

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Contains("may have been submitted", vm.ErrorMessage.Value, StringComparison.Ordinal);
        Assert.Null(vm.CreatedPullRequestNumber.Value);
        vm.Dispose();
    }
}
