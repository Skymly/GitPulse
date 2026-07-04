using GitPulse.Core.Models;
using Xunit;

namespace GitPulse.Tests;

public class IssueModelTests
{
    [Fact]
    public void Issue_Defaults_AreValid()
    {
        var issue = new Issue { Id = 1, Number = 42, Title = "Bug fix" };

        Assert.Equal(1, issue.Id);
        Assert.Equal(42, issue.Number);
        Assert.Equal("Bug fix", issue.Title);
        Assert.Equal(string.Empty, issue.State);
        Assert.Equal(string.Empty, issue.Body);
        Assert.Empty(issue.Labels);
        Assert.Equal(0, issue.CommentsCount);
        Assert.Null(issue.User);
        Assert.Null(issue.PullRequestRef);
        Assert.False(issue.IsPullRequest);
    }

    [Fact]
    public void Issue_WithPullRequestRef_IndicatesPR()
    {
        var issue = new Issue
        {
            Number = 10,
            Title = "Add feature",
            PullRequestRef = new PullRequestRef { HtmlUrl = "https://github.com/owner/repo/pull/10" },
        };

        Assert.NotNull(issue.PullRequestRef);
        Assert.Equal("https://github.com/owner/repo/pull/10", issue.PullRequestRef.HtmlUrl);
    }

    [Fact]
    public void Issue_WithLabels_StoresCollection()
    {
        var issue = new Issue
        {
            Number = 5,
            Title = "Test issue",
            Labels =
            [
                new Label { Name = "bug", Color = "ff0000" },
                new Label { Name = "help wanted", Color = "00ff00" },
            ],
        };

        Assert.Equal(2, issue.Labels.Length);
        Assert.Equal("bug", issue.Labels[0].Name);
        Assert.Equal("help wanted", issue.Labels[1].Name);
    }

    [Fact]
    public void Comment_Defaults_AreValid()
    {
        var comment = new Comment { Id = 100, Body = "Nice work!" };

        Assert.Equal(100, comment.Id);
        Assert.Equal("Nice work!", comment.Body);
        Assert.Equal(string.Empty, comment.HtmlUrl);
        Assert.Null(comment.User);
    }

    [Fact]
    public void PullRequest_Defaults_AreValid()
    {
        var pr = new PullRequest { Number = 1, Title = "Initial PR" };

        Assert.Equal(1, pr.Number);
        Assert.Equal("Initial PR", pr.Title);
        Assert.Equal(string.Empty, pr.State);
        Assert.False(pr.Draft);
        Assert.False(pr.Merged);
        Assert.Null(pr.User);
    }

    [Fact]
    public void Label_Defaults_AreValid()
    {
        var label = new Label { Name = "enhancement" };

        Assert.Equal("enhancement", label.Name);
        Assert.Equal(string.Empty, label.Color);
    }

    [Fact]
    public void Repo_Defaults_AreValid()
    {
        var repo = new Repo { Id = 1, Name = "gitpulse" };

        Assert.Equal(1, repo.Id);
        Assert.Equal("gitpulse", repo.Name);
        Assert.Equal(string.Empty, repo.FullName);
        Assert.Null(repo.Description);
        Assert.Equal(string.Empty, repo.HtmlUrl);
        Assert.False(repo.Private);
        Assert.Null(repo.DefaultBranch);
        Assert.Equal(0, repo.StargazersCount);
        Assert.Equal(0, repo.ForksCount);
        Assert.Equal(0, repo.OpenIssuesCount);
    }

    [Fact]
    public void User_Defaults_AreValid()
    {
        var user = new User { Id = 100, Login = "alice" };

        Assert.Equal(100, user.Id);
        Assert.Equal("alice", user.Login);
        Assert.Equal(string.Empty, user.AvatarUrl);
        Assert.Equal(string.Empty, user.HtmlUrl);
    }

    [Fact]
    public void PullRequestRef_Defaults_AreValid()
    {
        var prRef = new PullRequestRef { HtmlUrl = "https://github.com/o/r/pull/1" };

        Assert.Equal("https://github.com/o/r/pull/1", prRef.HtmlUrl);
        Assert.Equal(string.Empty, prRef.Url);
        Assert.Null(prRef.Merged);
        Assert.Null(prRef.DiffUrl);
    }

    [Fact]
    public void Comment_WithAllFields_PreservesValues()
    {
        var comment = new Comment
        {
            Id = 500,
            Body = "Reviewed",
            CreatedAt = new DateTime(2025, 1, 1),
            UpdatedAt = new DateTime(2025, 1, 2),
            User = new User { Login = "reviewer" },
            HtmlUrl = "https://github.com/o/r/issues/1#issuecomment-500",
        };

        Assert.Equal(500, comment.Id);
        Assert.Equal("Reviewed", comment.Body);
        Assert.Equal(new DateTime(2025, 1, 1), comment.CreatedAt);
        Assert.Equal(new DateTime(2025, 1, 2), comment.UpdatedAt);
        Assert.Equal("reviewer", comment.User!.Login);
        Assert.Equal("https://github.com/o/r/issues/1#issuecomment-500", comment.HtmlUrl);
    }
}
