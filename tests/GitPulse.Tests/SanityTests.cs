using GitPulse.Core.Models;
using Xunit;

namespace GitPulse.Tests;

public class SanityTests
{
    [Fact]
    public void Repo_Defaults_AreValid()
    {
        var repo = new Repo { Id = 1, Name = "test", FullName = "owner/test" };
        Assert.Equal(1, repo.Id);
        Assert.Equal("test", repo.Name);
        Assert.Equal("owner/test", repo.FullName);
        Assert.Equal(string.Empty, repo.HtmlUrl);
    }

    [Fact]
    public void Issue_Defaults_AreValid()
    {
        var issue = new Issue { Number = 42, Title = "Bug" };
        Assert.Equal(42, issue.Number);
        Assert.Equal("Bug", issue.Title);
        Assert.Equal(string.Empty, issue.State);
    }
}
