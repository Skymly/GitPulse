using System.Text.Json;
using GitPulse.Core.Models;
using Xunit;

namespace GitPulse.Tests;

public class SearchModelTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void SearchResult_DeserializesGenericWrapperAndSnakeCaseFields()
    {
        const string json = """
            {
              "total_count": 42,
              "incomplete_results": true,
              "items": [
                {
                  "id": 1,
                  "name": "GitPulse",
                  "full_name": "Skymly/GitPulse",
                  "html_url": "https://github.com/Skymly/GitPulse",
                  "stargazers_count": 7
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<SearchResult<Repo>>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(42, result.TotalCount);
        Assert.True(result.IncompleteResults);
        var repo = Assert.Single(result.Items);
        Assert.Equal("Skymly/GitPulse", repo.FullName);
        Assert.Equal(7, repo.StargazersCount);
    }

    [Fact]
    public void SearchIssueItem_DeserializesRepositoryUrlAndPullRequestMarker()
    {
        const string json = """
            {
              "id": 2,
              "number": 17,
              "title": "Search result",
              "state": "open",
              "html_url": "https://github.com/Skymly/GitPulse/pull/17",
              "repository_url": "https://api.github.com/repos/Skymly/GitPulse",
              "created_at": "2026-07-10T01:02:03Z",
              "updated_at": "2026-07-11T04:05:06Z",
              "pull_request": {
                "url": "https://api.github.com/repos/Skymly/GitPulse/pulls/17"
              }
            }
            """;

        var item = JsonSerializer.Deserialize<SearchIssueItem>(json, JsonOptions);

        Assert.NotNull(item);
        Assert.Equal("https://api.github.com/repos/Skymly/GitPulse", item.RepositoryUrl);
        Assert.Equal(17, item.Number);
        Assert.NotNull(item.PullRequest);
        Assert.Equal(new DateTime(2026, 7, 10, 1, 2, 3, DateTimeKind.Utc), item.CreatedAt);
    }

    [Fact]
    public void CodeSearchItem_DeserializesRepositoryPathAndSha()
    {
        const string json = """
            {
              "name": "SearchViewModel.cs",
              "path": "src/GitPulse.ViewModels/SearchViewModel.cs",
              "sha": "abc123",
              "html_url": "https://github.com/Skymly/GitPulse/blob/abc123/SearchViewModel.cs",
              "repository": {
                "id": 1,
                "name": "GitPulse",
                "full_name": "Skymly/GitPulse",
                "html_url": "https://github.com/Skymly/GitPulse"
              }
            }
            """;

        var item = JsonSerializer.Deserialize<CodeSearchItem>(json, JsonOptions);

        Assert.NotNull(item);
        Assert.Equal("src/GitPulse.ViewModels/SearchViewModel.cs", item.Path);
        Assert.Equal("abc123", item.Sha);
        Assert.Equal("Skymly/GitPulse", item.Repository.FullName);
    }
}
