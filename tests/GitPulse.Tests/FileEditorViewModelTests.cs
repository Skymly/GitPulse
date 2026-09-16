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
        FileContentsJson(name, path, sha, contentBase64.Length, "base64", contentBase64);

    /// <summary>
    /// GitHub Contents file JSON. Pass <paramref name="content"/> as the raw
    /// payload string, or <c>null</c> for JSON null.
    /// </summary>
    private static string FileContentsJson(
        string name,
        string path,
        string sha,
        long size,
        string encoding,
        string? content)
    {
        var contentJson = content is null ? "null" : $"\"{content}\"";
        return $"{{\"name\":\"{name}\",\"path\":\"{path}\",\"sha\":\"{sha}\"," +
            $"\"size\":{size},\"content\":{contentJson}," +
            $"\"encoding\":\"{encoding}\"," +
            $"\"html_url\":\"https://github.com/o/r/blob/HEAD/{path}\"," +
            $"\"download_url\":\"https://raw.githubusercontent.com/o/r/HEAD/{path}\"}}";
    }

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
    public async Task Delete_WithToken_ClearsContentAndLeavesViewMode()
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

        Assert.Equal("", vm.FileContent.Value);
        Assert.False(vm.IsEditing.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Delete_WithoutCommitMessage_SetsErrorMessage()
    {
        var deletes = 0;
        var handler = new MockHttpHandler()
            .When(HttpMethod.Delete, "/contents/file.txt", _ =>
            {
                deletes++;
                return new MockResponse("{}");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "sha-123");

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Contains("commit message", vm.ErrorMessage.Value);
        Assert.Equal(0, deletes);
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

    [Fact]
    public void Initialize_WithGitRef_SetsReadOnlyAndNotNew()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());

        vm.Initialize("owner", "repo", "src/Program.cs", sha: null, gitRef: "abc123");

        Assert.True(vm.IsReadOnly.Value);
        Assert.False(vm.IsNewFile.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithGitRef_RequestsContentsAtRef()
    {
        HttpRequestMessage? seen = null;
        var content = B64("at commit");
        var handler = new MockHttpHandler()
            .When("/contents/README.md", req =>
            {
                seen = req;
                return new MockResponse(FileJson("README.md", "README.md", "blob-sha", content));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "README.md", sha: null, gitRef: "abc123");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotNull(seen);
        Assert.Contains("ref=abc123", seen!.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal("at commit", vm.FileContent.Value);
        Assert.True(vm.IsReadOnly.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Save_WhenReadOnly_IsNoOp()
    {
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", FileJson("file.txt", "file.txt", "blob", B64("x")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", sha: null, gitRef: "abc123");
        await vm.LoadCommand.ExecuteAsync(null);
        vm.CommitMessage.Value = "should not save";
        vm.FileContent.Value = "changed";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.False(vm.IsEditing.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenEncodingNone_SetsReadOnlyAndDoesNotSave()
    {
        var puts = 0;
        var handler = new MockHttpHandler()
            .When(HttpMethod.Get, "/contents/big.bin", FileContentsJson(
                "big.bin", "big.bin", "blob-sha", size: 100, encoding: "none", content: ""))
            .When(HttpMethod.Put, "/contents/big.bin", _ =>
            {
                puts++;
                return new MockResponse(CommitJson("should-not-write"));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "big.bin", "blob-sha");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsReadOnly.Value);
        Assert.False(vm.IsEditing.Value);
        Assert.Contains("GitHub", vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);

        vm.FileContent.Value = "should not persist";
        vm.CommitMessage.Value = "truncate";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, puts);
        Assert.True(vm.IsReadOnly.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenSizeOverOneMegabyte_SetsReadOnlyAndDoesNotSave()
    {
        var puts = 0;
        var handler = new MockHttpHandler()
            .When(HttpMethod.Get, "/contents/large.bin", FileContentsJson(
                "large.bin", "large.bin", "blob-sha", size: 2_000_000, encoding: "base64", content: ""))
            .When(HttpMethod.Put, "/contents/large.bin", _ =>
            {
                puts++;
                return new MockResponse(CommitJson("should-not-write"));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "large.bin", "blob-sha");

        await vm.LoadCommand.ExecuteAsync(null);
        vm.FileContent.Value = "should not persist";
        vm.CommitMessage.Value = "truncate";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.IsReadOnly.Value);
        Assert.Equal(0, puts);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenContentNull_SetsReadOnlyWithoutThrowing()
    {
        var puts = 0;
        var handler = new MockHttpHandler()
            .When(HttpMethod.Get, "/contents/empty.bin", FileContentsJson(
                "empty.bin", "empty.bin", "blob-sha", size: 50, encoding: "base64", content: null))
            .When(HttpMethod.Put, "/contents/empty.bin", _ =>
            {
                puts++;
                return new MockResponse(CommitJson("should-not-write"));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "empty.bin", "blob-sha");

        await vm.LoadCommand.ExecuteAsync(null);
        vm.CommitMessage.Value = "truncate";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Load failed", vm.ErrorMessage.Value, StringComparison.Ordinal);
        Assert.True(vm.IsReadOnly.Value);
        Assert.False(vm.IsEditing.Value);
        Assert.Equal(0, puts);
        vm.Dispose();
    }

    [Fact]
    public async Task Delete_WhenReadOnly_IsNoOp()
    {
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", FileJson("file.txt", "file.txt", "blob", B64("x")));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", sha: null, gitRef: "abc123");
        await vm.LoadCommand.ExecuteAsync(null);
        vm.CommitMessage.Value = "should not delete";

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("x", vm.FileContent.Value);
        vm.Dispose();
    }

    [Fact]
    public void ToggleEdit_WhenReadOnly_StaysViewMode()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", sha: null, gitRef: "abc123");

        vm.ToggleEditCommand.Execute(null);

        Assert.False(vm.IsEditing.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_PngBytes_MarksBinaryAndDisablesEditing()
    {
        var png = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var handler = new MockHttpHandler()
            .When("/contents/icon.png", FileJson("icon.png", "icon.png", "blob-png", png));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "icon.png", "blob-png");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsBinary.Value);
        Assert.False(vm.IsEditing.Value);
        Assert.Contains("Binary file", vm.FileContent.Value, StringComparison.Ordinal);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_NulBytes_MarksBinary()
    {
        var binary = Convert.ToBase64String(new byte[] { 0x48, 0x00, 0x69 });
        var handler = new MockHttpHandler()
            .When("/contents/data.bin", FileJson("data.bin", "data.bin", "blob-bin", binary));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "data.bin", "blob-bin");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsBinary.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Save_BinaryFile_IsNoOp()
    {
        var png = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        HttpRequestMessage? put = null;
        var handler = new MockHttpHandler()
            .When("/contents/icon.png", req =>
            {
                if (req.Method == HttpMethod.Put)
                {
                    put = req;
                    return new MockResponse(CommitJson("should-not-save"));
                }

                return new MockResponse(FileJson("icon.png", "icon.png", "blob-png", png));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "icon.png", "blob-png");
        await vm.LoadCommand.ExecuteAsync(null);
        vm.CommitMessage.Value = "corrupt the png";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(put);
        Assert.True(vm.IsBinary.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Save_WhenContentShaMissing_DoesNotUseCommitShaOnNextSave()
    {
        var puts = new List<string>();
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", req =>
            {
                if (req.Method == HttpMethod.Put)
                {
                    puts.Add(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                    return new MockResponse(
                        "{\"content\":null,\"commit\":{\"sha\":\"commit-not-blob\"," +
                        "\"html_url\":\"https://github.com/o/r/commit/x\",\"message\":\"Update file\"}}");
                }

                return new MockResponse(FileJson("file.txt", "file.txt", "blob-original", B64("old")));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "blob-original");
        await vm.LoadCommand.ExecuteAsync(null);

        vm.FileContent.Value = "first";
        vm.CommitMessage.Value = "first save";
        await vm.SaveCommand.ExecuteAsync(null);

        vm.IsEditing.Value = true;
        vm.FileContent.Value = "second";
        vm.CommitMessage.Value = "second save";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(2, puts.Count);
        Assert.Contains("blob-original", puts[1], StringComparison.Ordinal);
        Assert.DoesNotContain("commit-not-blob", puts[1], StringComparison.Ordinal);
        vm.Dispose();
    }

    [Fact]
    public async Task OpenInBrowser_EncodesPathSegments()
    {
        var launcher = new FakeBrowserLauncher();
        var vm = new FileEditorViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), launcher);
        vm.Initialize("own er", "re/po", "src/foo bar.cs");

        await vm.OpenInBrowserCommand.ExecuteAsync(null);

        Assert.Equal(
            "https://github.com/own%20er/re%2Fpo/blob/HEAD/src/foo%20bar.cs",
            Assert.Single(launcher.OpenedUrls));
        vm.Dispose();
    }

    [Fact]
    public async Task Save_DuringLoad_DoesNotPut()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var puts = 0;
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", req =>
            {
                if (req.Method == HttpMethod.Put)
                {
                    puts++;
                    return new MockResponse(CommitJson("saved"));
                }

                return new MockResponse(
                    FileJson("file.txt", "file.txt", "blob-1", B64("old")),
                    Gate: gate.Task);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "blob-1");

        var load = vm.LoadCommand.ExecuteAsync(null);
        await AsyncTestWait.UntilAsync(() => vm.IsBusy.Value);
        vm.FileContent.Value = "edited";
        vm.CommitMessage.Value = "save during load";
        vm.IsEditing.Value = true;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, puts);

        gate.SetResult();
        await load;
        vm.Dispose();
    }

    [Fact]
    public async Task Load_DuringSave_DoesNotGetAgain()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gets = 0;
        var handler = new MockHttpHandler()
            .When("/contents/file.txt", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse(CommitJson("saved"), Gate: gate.Task);

                gets++;
                return new MockResponse(FileJson("file.txt", "file.txt", "blob-1", B64("old")));
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new FileEditorViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "file.txt", "blob-1");
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(1, gets);

        vm.FileContent.Value = "edited";
        vm.CommitMessage.Value = "save";
        vm.IsEditing.Value = true;
        var save = vm.SaveCommand.ExecuteAsync(null);
        await AsyncTestWait.UntilAsync(() => vm.IsBusy.Value);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(1, gets);

        gate.SetResult();
        await save;
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
