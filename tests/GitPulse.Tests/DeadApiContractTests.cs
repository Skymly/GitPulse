using GitPulse.GitHubApi;
using Xunit;

namespace GitPulse.Tests;

public class DeadApiContractTests
{
    [Fact]
    public void IGitHubReposApi_DoesNotExposeUnsortedListMyReposPaged()
    {
        Assert.Null(typeof(IGitHubReposApi).GetMethod("ListMyReposPaged"));
        Assert.NotNull(typeof(IGitHubReposApi).GetMethod(nameof(IGitHubReposApi.ListMyReposSortedPaged)));
    }

    [Fact]
    public void IGitHubActionsApi_DoesNotExposeGetJobLogs()
    {
        Assert.Null(typeof(IGitHubActionsApi).GetMethod("GetJobLogs"));
    }
}
