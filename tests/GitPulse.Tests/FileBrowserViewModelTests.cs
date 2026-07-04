using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class FileBrowserViewModelTests
{
    private static string DirJson(params (string name, string type, string path)[] entries)
    {
        var items = entries.Select(e =>
            $"{{\"name\":\"{e.name}\",\"path\":\"{e.path}\"," +
            $"\"type\":\"{e.type}\",\"sha\":\"sha-{e.name}\"," +
            $"\"size\":{(e.type == "file" ? "1024" : "0")}," +
            $"\"url\":\"https://api.github.com/repos/o/r/contents/{e.path}\"," +
            $"\"html_url\":\"https://github.com/o/r/blob/HEAD/{e.path}\"}}");
        return $"[{string.Join(",", items)}]";
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Entries);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_PopulatesEntriesSortedDirsFirst()
    {
        var handler = new MockHttpHandler()
            .When("/contents", DirJson(
                ("README.md", "file", "README.md"),
                ("src", "dir", "src"),
                ("tests", "dir", "tests"),
                (".gitignore", "file", ".gitignore")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(4, vm.Entries.Count);
        // Directories first, then files, each alphabetical.
        Assert.Equal("src", vm.Entries[0].Name);
        Assert.Equal("tests", vm.Entries[1].Name);
        Assert.Equal(".gitignore", vm.Entries[2].Name);
        Assert.Equal("README.md", vm.Entries[3].Name);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithEmptyOwner_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/contents", DirJson(("file.txt", "file", "file.txt")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        // Don't call Initialize.

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Entries);
        Assert.False(vm.IsLoading.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task NavigateTo_Directory_UpdatesPathAndReloads()
    {
        var handler = new MockHttpHandler()
            .When("/contents", DirJson(("src", "dir", "src")))
            .When("/contents/src", DirJson(("Program.cs", "file", "src/Program.cs")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal("src", vm.Entries[0].Name);

        await vm.NavigateToCommand.ExecuteAsync(vm.Entries[0]);

        Assert.Equal("src", vm.CurrentPath.Value);
        Assert.True(vm.CanGoUp.Value);
        Assert.Single(vm.Entries);
        Assert.Equal("Program.cs", vm.Entries[0].Name);
        vm.Dispose();
    }

    [Fact]
    public async Task GoUp_FromSubdirectory_ReturnsToRoot()
    {
        var handler = new MockHttpHandler()
            .When("/contents", DirJson(("src", "dir", "src")))
            .When("/contents/src", DirJson(("Program.cs", "file", "src/Program.cs")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.NavigateToCommand.ExecuteAsync(vm.Entries[0]);
        Assert.Equal("src", vm.CurrentPath.Value);

        await vm.GoUpCommand.ExecuteAsync(null);

        Assert.Equal("", vm.CurrentPath.Value);
        Assert.False(vm.CanGoUp.Value);
        Assert.Single(vm.Entries); // Root has "src" dir
        vm.Dispose();
    }

    [Fact]
    public async Task GoUp_FromRoot_IsNoOp()
    {
        var handler = new MockHttpHandler()
            .When("/contents", DirJson(("file.txt", "file", "file.txt")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.GoUpCommand.ExecuteAsync(null);

        Assert.Equal("", vm.CurrentPath.Value);
        Assert.False(vm.CanGoUp.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task GoUp_FromNestedDirectory_GoesOneLevelUp()
    {
        var handler = new MockHttpHandler()
            .When("/contents", DirJson(("src", "dir", "src")))
            .When("/contents/src", DirJson(("Models", "dir", "src/Models")))
            .When("/contents/src/Models", DirJson(("Repo.cs", "file", "src/Models/Repo.cs")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.NavigateToCommand.ExecuteAsync(vm.Entries[0]); // → src
        await vm.NavigateToCommand.ExecuteAsync(vm.Entries[0]); // → src/Models
        Assert.Equal("src/Models", vm.CurrentPath.Value);

        await vm.GoUpCommand.ExecuteAsync(null);

        Assert.Equal("src", vm.CurrentPath.Value);
        Assert.True(vm.CanGoUp.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithNotFoundResponse_SetsErrorMessage()
    {
        var handler = new MockHttpHandler(); // No routes → 404
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("Load failed", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public void Initialize_SetsAllProperties()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());

        vm.Initialize("owner", "repo", "src/Models");

        Assert.Equal("owner/repo", vm.RepoFullName.Value);
        Assert.Equal("src/Models", vm.CurrentPath.Value);
        Assert.True(vm.CanGoUp.Value);
        Assert.Equal("owner", vm.Owner.Value);
        Assert.Equal("repo", vm.RepoName.Value);
        vm.Dispose();
    }

    [Fact]
    public void Initialize_WithEmptyPath_SetsCanGoUpFalse()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileBrowserViewModel(factory, new FakeBrowserLauncher());

        vm.Initialize("owner", "repo", "");

        Assert.Equal("", vm.CurrentPath.Value);
        Assert.False(vm.CanGoUp.Value);
        vm.Dispose();
    }
}
