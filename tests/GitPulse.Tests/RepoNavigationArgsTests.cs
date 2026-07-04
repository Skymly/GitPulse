using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class RepoNavigationArgsTests
{
    [Fact]
    public void Constructor_SetsOwnerAndRepo()
    {
        var args = new RepoNavigationArgs("Skymly", "GitPulse");

        Assert.Equal("Skymly", args.Owner);
        Assert.Equal("GitPulse", args.Repo);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new RepoNavigationArgs("owner", "repo");
        var b = new RepoNavigationArgs("owner", "repo");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new RepoNavigationArgs("owner", "repo1");
        var b = new RepoNavigationArgs("owner", "repo2");

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}
