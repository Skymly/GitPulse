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
}
