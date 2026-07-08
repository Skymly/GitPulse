using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>File path and line for starting a new review comment.</summary>
public readonly record struct CommentTarget(string FilePath, int Line);

/// <summary>
/// PR diff view model — the M8 RestAPI domain showcase. Loads changed
/// files (<see cref="IGitHubReposApi.ListPullRequestFiles"/>) and inline
/// review comments (<see cref="IGitHubReposApi.ListReviewComments"/>),
/// and supports creating new review comments and replies
/// (<see cref="IGitHubReposApi.CreateReviewComment"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>File grouping:</b> Review comments are grouped by file path via
/// <see cref="FileComments"/>. The UI renders each file's diff with its
/// comments inline below the diff block.
/// </para>
/// <para>
/// <b>Comment creation:</b> New top-level comments require
/// <c>commit_id</c>, <c>path</c>, <c>line</c>, and <c>side</c>. Replies
/// only need <c>body</c> and <c>in_reply_to</c>.
/// </para>
/// </remarks>
public sealed partial class PrDiffViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private int _prNumber;
    private string _headSha = string.Empty;

    /// <summary>Changed files in the pull request.</summary>
    public ObservableCollection<DiffEntry> Files { get; } = [];

    /// <summary>
    /// Review comments grouped by file path. Key is the file path,
    /// value is the list of comments on that file.
    /// </summary>
    public Dictionary<string, List<ReviewComment>> FileComments { get; } = [];

    /// <summary>Whether files + comments are loading.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Whether a comment is being posted.</summary>
    public BindableReactiveProperty<bool> IsSaving { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>
    /// Comment input text for the currently selected file/line.
    /// Two-way bound to the editor.
    /// </summary>
    public BindableReactiveProperty<string> CommentInput { get; } = new(string.Empty);

    /// <summary>File path for the comment being written (selected file).</summary>
    public BindableReactiveProperty<string> CommentFilePath { get; } = new(string.Empty);

    /// <summary>Line number for the comment being written.</summary>
    public BindableReactiveProperty<int> CommentLine { get; } = new(0);

    /// <summary>Comment ID being replied to (0 for top-level comments).</summary>
    public BindableReactiveProperty<long> ReplyToId { get; } = new(0);

    public PrDiffViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public void Initialize(string owner, string repo, int prNumber, string headSha)
    {
        _owner = owner;
        _repo = repo;
        _prNumber = prNumber;
        _headSha = headSha;
    }

    /// <summary>Load changed files and review comments in parallel.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || _prNumber <= 0)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var filesTask = api.ListPullRequestFiles(_owner, _repo, _prNumber).FirstAsync(cts.Token);
            var commentsTask = api.ListReviewComments(_owner, _repo, _prNumber).FirstAsync(cts.Token);

            await Task.WhenAll(filesTask, commentsTask);

            Files.Clear();
            foreach (var f in filesTask.Result)
                Files.Add(f);

            FileComments.Clear();
            foreach (var c in commentsTask.Result)
            {
                if (!FileComments.TryGetValue(c.Path, out var list))
                {
                    list = [];
                    FileComments[c.Path] = list;
                }
                list.Add(c);
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    /// <summary>
    /// Start writing a new top-level comment on a specific file/line.
    /// Called by the UI when the user taps "comment" on a diff line.
    /// </summary>
    [RelayCommand]
    private void StartComment(CommentTarget location)
    {
        CommentFilePath.Value = location.FilePath;
        CommentLine.Value = location.Line;
        ReplyToId.Value = 0;
        CommentInput.Value = string.Empty;
    }

    /// <summary>
    /// Start writing a reply to an existing review comment.
    /// </summary>
    [RelayCommand]
    private void StartReply(long commentId)
    {
        ReplyToId.Value = commentId;
        CommentInput.Value = string.Empty;
    }

    /// <summary>
    /// Post the current comment. Creates a top-level comment or a reply
    /// depending on <see cref="ReplyToId"/>.
    /// </summary>
    [RelayCommand]
    private async Task PostCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(CommentInput.Value) || IsSaving.Value)
            return;

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var request = new ReviewCommentRequest
            {
                Body = CommentInput.Value,
            };

            if (ReplyToId.Value > 0)
            {
                // Reply: only body + in_reply_to are needed.
                request.InReplyTo = ReplyToId.Value;
            }
            else
            {
                // Top-level comment: needs commit_id, path, line, side.
                request.CommitId = _headSha;
                request.Path = CommentFilePath.Value;
                request.Line = CommentLine.Value;
                request.Side = "RIGHT";
            }

            var comment = await api.CreateReviewComment(_owner, _repo, _prNumber, request)
                .FirstAsync(cts.Token);

            // Add to the appropriate file group.
            if (!FileComments.TryGetValue(comment.Path, out var list))
            {
                list = [];
                FileComments[comment.Path] = list;
            }
            list.Add(comment);

            CommentInput.Value = string.Empty;
            ReplyToId.Value = 0;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Comment failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    /// <summary>Get comments for a specific file path (empty list if none).</summary>
    public List<ReviewComment> GetCommentsForFile(string path)
    {
        return FileComments.TryGetValue(path, out var list) ? list : [];
    }

    public void Dispose()
    {
        IsLoading.Dispose();
        IsSaving.Dispose();
        ErrorMessage.Dispose();
        CommentInput.Dispose();
        CommentFilePath.Dispose();
        CommentLine.Dispose();
        ReplyToId.Dispose();
    }
}
