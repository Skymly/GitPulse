using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class FileEditorViewModelTests
{
    /// <summary>Base64-encode a UTF-8 string for mock file content responses.</summary>
    private static string B64(string text) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));

    private static string FileJson(string name, string path, string sha, string contentBase64) =>
        $"{{\"name\":\"{name}\",\"path\":\"{path}\",\"sha\":\"{sha}\"," +
        $"\"size\":{contentBase64.Length},\"content\":\"{contentBase64}\"," +
        $"\"encoding\":\"base64\"," +
        $"\"html_url\":\"https://github.com/o/r/blob/HEAD/{path}\"," +
        $"\"download_url\":\"https://raw.githubusercontent.com/o/r/HEAD/{path}\"}}";

    private static string CommitJson(string sha) =>
        $"{{\"content\":{{\"name\":\"file.txt\",\"path\":\"file.txt\"," +
        $"\"sha\":\"{sha}\",\"size\":100,\"content\":\"\",\"encoding\":\"base64\"}}," +
        $"\"commit\":{{\"sha\":\"commit-{sha}\",\"html_url\":\"https://github.com/o/r/commit/{sha}\"," +
        $"\"message\":\"Update file\"}}}}";

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "README.md", "sha-123");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Equal("", vm.FileContent.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_DecodesBase64Content()
    {
        var content = B64("# Hello World\nThis is a test file.");
        var handler = new MockHttpHandler()
            .When("/contents/README.md", FileJson("README.md", "README.md", "sha-123", content));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "README.md", "sha-123");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("# Hello World\nThis is a test file.", vm.FileContent.Value);
        Assert.False(vm.IsBinary.Value);
        Assert.False(vm.IsNewFile.Value);
        Assert.Equal("README.md", vm.Title.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_NewFile_EntersEditMode()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "new-file.txt", sha: null);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsEditing.Value);
        Assert.True(vm.IsNewFile.Value);
        Assert.Equal("", vm.FileContent.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithNotFoundResponse_SetsErrorMessage()
    {
        var handler = new MockHttpHandler(); // No routes → 404
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "missing.txt", "sha-123");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("Load failed", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Save_WithoutCommitMessage_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", _ => new MockResponse(CommitJson("new-sha")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", sha: null);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.FileContent.Value = "new content";
        // CommitMessage is empty by default.
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("commit message", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Save_WithToken_UpdatesFileAndClearsEditState()
    {
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse(CommitJson("new-sha-456"));
                var content = B64("old content");
                return new MockResponse(FileJson("file.txt", "file.txt", "sha-123", content));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "sha-123");
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal("old content", vm.FileContent.Value);

        vm.FileContent.Value = "new content";
        vm.CommitMessage.Value = "Update file";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.False(vm.IsEditing.Value);
        Assert.Equal("", vm.CommitMessage.Value);
        Assert.False(vm.IsNewFile.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Save_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "sha-123");

        vm.FileContent.Value = "content";
        vm.CommitMessage.Value = "Update";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Delete_WithToken_RemovesFileAndShowsMessage()
    {
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", req =>
            {
                if (req.Method == HttpMethod.Delete)
                    return new MockResponse(CommitJson("delete-sha"));
                var content = B64("content");
                return new MockResponse(FileJson("file.txt", "file.txt", "sha-123", content));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "sha-123");
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CommitMessage.Value = "Delete file";
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Contains("deleted", vm.ErrorMessage.Value);
        Assert.Equal("", vm.FileContent.Value);
        Assert.False(vm.IsEditing.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Delete_WithoutCommitMessage_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "sha-123");

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Contains("commit message", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Delete_NewFile_IsNoOp()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "new.txt", sha: null);

        vm.CommitMessage.Value = "Delete";
        await vm.DeleteCommand.ExecuteAsync(null);

        // Should not attempt deletion for a new file.
        Assert.DoesNotContain("deleted", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public void ToggleEdit_SwitchesBetweenViewAndEditModes()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "sha-123");

        Assert.False(vm.IsEditing.Value);
        vm.ToggleEditCommand.Execute(null);
        Assert.True(vm.IsEditing.Value);
        vm.ToggleEditCommand.Execute(null);
        Assert.False(vm.IsEditing.Value);
        vm.Dispose();
    }

    [Fact]
    public void Initialize_WithSha_SetsIsNewFileFalse()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());

        vm.Initialize("owner", "repo", "src/Program.cs", "sha-abc");

        Assert.False(vm.IsNewFile.Value);
        Assert.Equal("Program.cs", vm.FileName.Value);
        Assert.Equal("src/Program.cs", vm.FilePath.Value);
        Assert.Equal("owner/repo", vm.RepoFullName.Value);
        Assert.Equal("Program.cs", vm.Title.Value);
        vm.Dispose();
    }

    [Fact]
    public void Initialize_WithoutSha_SetsIsNewFileTrue()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());

        vm.Initialize("owner", "repo", "new-file.txt", sha: null);

        Assert.True(vm.IsNewFile.Value);
        Assert.Equal("New: new-file.txt", vm.Title.Value);
        vm.Dispose();
    }
}

public class ContentModelTests
{
    [Fact]
    public void ContentEntry_Defaults_AreValid()
    {
        var entry = new ContentEntry { Name = "file.txt" };

        Assert.Equal("file.txt", entry.Name);
        Assert.Equal(string.Empty, entry.Path);
        Assert.Equal(string.Empty, entry.Type);
        Assert.Equal(string.Empty, entry.Sha);
        Assert.Equal(0, entry.Size);
        Assert.Equal(string.Empty, entry.Url);
        Assert.Equal(string.Empty, entry.HtmlUrl);
        Assert.Null(entry.DownloadUrl);
    }

    [Fact]
    public void FileContent_Defaults_AreValid()
    {
        var fc = new FileContent { Name = "test.cs" };

        Assert.Equal("test.cs", fc.Name);
        Assert.Equal(string.Empty, fc.Path);
        Assert.Equal(string.Empty, fc.Sha);
        Assert.Equal(0, fc.Size);
        Assert.Equal(string.Empty, fc.Content);
        Assert.Equal(string.Empty, fc.Encoding);
        Assert.Equal(string.Empty, fc.HtmlUrl);
        Assert.Null(fc.DownloadUrl);
    }

    [Fact]
    public void FileUpdateRequest_Defaults_AreValid()
    {
        var req = new FileUpdateRequest { Message = "msg", Content = "base64" };

        Assert.Equal("msg", req.Message);
        Assert.Equal("base64", req.Content);
        Assert.Null(req.Sha);
        Assert.Null(req.Branch);
    }

    [Fact]
    public void FileDeleteRequest_Defaults_AreValid()
    {
        var req = new FileDeleteRequest { Message = "delete", Sha = "abc" };

        Assert.Equal("delete", req.Message);
        Assert.Equal("abc", req.Sha);
        Assert.Null(req.Branch);
    }

    [Fact]
    public void FileCommitResponse_Defaults_AreValid()
    {
        var resp = new FileCommitResponse();

        Assert.Null(resp.Content);
        Assert.Null(resp.Commit);
    }

    [Fact]
    public void FileCommit_Defaults_AreValid()
    {
        var commit = new FileCommit { Sha = "abc123" };

        Assert.Equal("abc123", commit.Sha);
        Assert.Equal(string.Empty, commit.HtmlUrl);
        Assert.Equal(string.Empty, commit.Message);
    }
}
