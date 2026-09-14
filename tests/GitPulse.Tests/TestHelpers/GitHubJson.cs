namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// GitHub REST JSON shaped like the public docs (octocat / Hello-World samples).
/// Nested <c>head.ref</c> / <c>base.ref</c>, snake_case timestamps and urls.
/// Never emits the fabricated <c>headRef</c> / <c>baseRef</c> keys.
/// </summary>
internal static class GitHubJson
{
    public const string CreatedAt = "2011-01-26T19:01:12Z";
    public const string CommentCreatedAt = "2011-04-14T16:00:49Z";

    public static string User(string login) =>
        $"{{\"login\":\"{login}\",\"id\":1,\"avatar_url\":\"https://github.com/images/error/{login}_happy.gif\",\"html_url\":\"https://github.com/{login}\"}}";

    public static string Comment(long id, string body, string login = "alice") =>
        $"{{\"id\":{id},\"body\":\"{body}\",\"user\":{User(login)}," +
        $"\"created_at\":\"{CommentCreatedAt}\",\"updated_at\":\"{CommentCreatedAt}\"," +
        $"\"html_url\":\"https://github.com/octocat/Hello-World/issues/1347#issuecomment-{id}\"}}";

    public static string Issue(
        int number,
        string state = "open",
        string? body = "body",
        string titlePrefix = "Issue",
        string login = "alice",
        int comments = 0,
        string assigneesJson = "[]",
        string labelsJson = "[]",
        string? title = null,
        bool pullRequest = false)
    {
        var issueBody = body ?? "";
        var issueTitle = title ?? $"{titlePrefix} {number}";
        var pr = pullRequest
            ? ",\"pull_request\":{" +
              $"\"url\":\"https://api.github.com/repos/octocat/Hello-World/pulls/{number}\"," +
              $"\"html_url\":\"https://github.com/octocat/Hello-World/pull/{number}\"," +
              $"\"diff_url\":\"https://github.com/octocat/Hello-World/pull/{number}.diff\"" +
              "}"
            : "";
        return "{" +
            $"\"number\":{number}," +
            $"\"title\":\"{issueTitle}\"," +
            $"\"state\":\"{state}\"," +
            $"\"body\":\"{issueBody}\"," +
            $"\"html_url\":\"https://github.com/octocat/Hello-World/issues/{number}\"," +
            $"\"created_at\":\"{CreatedAt}\"," +
            $"\"updated_at\":\"{CreatedAt}\"," +
            $"\"comments\":{comments}," +
            $"\"user\":{User(login)}," +
            $"\"labels\":{labelsJson}," +
            $"\"assignees\":{assigneesJson}" +
            pr +
            "}";
    }

    public static string PullRequest(
        int number,
        string state = "open",
        bool draft = false,
        bool merged = false,
        bool? mergeable = null,
        string? mergeableState = null,
        int commits = 0,
        int additions = 0,
        int deletions = 0,
        int changedFiles = 0,
        string? title = null,
        string body = "",
        string? headSha = null,
        string headRef = "new-topic",
        string baseRef = "master",
        string login = "bob",
        string? assigneesJson = null,
        string? labelsJson = null,
        bool includeHeadSha = true,
        string? mergeCommitSha = null,
        string? mergedBy = null)
    {
        var sha = headSha ?? "6dcb09b5b57875f334f61aebed695e2e4193db5e";
        var head = includeHeadSha
            ? $"{{\"label\":\"{login}:{headRef}\",\"ref\":\"{headRef}\",\"sha\":\"{sha}\",\"user\":{User(login)}}}"
            : $"{{\"label\":\"{login}:{headRef}\",\"ref\":\"{headRef}\",\"user\":{User(login)}}}";
        var @base = includeHeadSha
            ? $"{{\"label\":\"{login}:{baseRef}\",\"ref\":\"{baseRef}\",\"sha\":\"{sha}\",\"user\":{User(login)}}}"
            : $"{{\"label\":\"{login}:{baseRef}\",\"ref\":\"{baseRef}\",\"user\":{User(login)}}}";
        var json = "{" +
            $"\"number\":{number}," +
            $"\"title\":\"{title ?? $"PR {number}"}\"," +
            $"\"state\":\"{state}\"," +
            $"\"body\":\"{body}\"," +
            $"\"draft\":{Bool(draft)}," +
            $"\"merged\":{Bool(merged)}," +
            $"\"html_url\":\"https://github.com/octocat/Hello-World/pull/{number}\"," +
            $"\"created_at\":\"{CreatedAt}\"," +
            $"\"updated_at\":\"{CreatedAt}\"," +
            $"\"commits\":{commits}," +
            $"\"additions\":{additions}," +
            $"\"deletions\":{deletions}," +
            $"\"changed_files\":{changedFiles}," +
            $"\"head\":{head}," +
            $"\"base\":{@base}," +
            $"\"user\":{User(login)}";

        if (mergeable.HasValue)
            json += $",\"mergeable\":{Bool(mergeable.Value)}";
        if (mergeableState is not null)
            json += $",\"mergeable_state\":\"{mergeableState}\"";
        if (assigneesJson is not null)
            json += $",\"assignees\":{assigneesJson}";
        if (labelsJson is not null)
            json += $",\"labels\":{labelsJson}";
        if (mergeCommitSha is not null)
            json += $",\"merge_commit_sha\":\"{mergeCommitSha}\"";
        if (mergedBy is not null)
            json += $",\"merged_by\":{User(mergedBy)}";

        return json + "}";
    }

    /// <summary>
    /// Unmodified Get-a-pull-request example from GitHub REST docs (trimmed to
    /// fields GitPulse models). Field names are the documented snake_case keys.
    /// </summary>
    public const string OfficialPullRequest = """
        {
          "url": "https://api.github.com/repos/octocat/Hello-World/pulls/1347",
          "id": 1,
          "number": 1347,
          "state": "open",
          "locked": false,
          "title": "new-feature",
          "user": {
            "login": "octocat",
            "id": 1,
            "avatar_url": "https://github.com/images/error/octocat_happy.gif",
            "html_url": "https://github.com/octocat"
          },
          "body": "Please pull these awesome changes",
          "created_at": "2011-01-26T19:01:12Z",
          "updated_at": "2011-01-26T19:01:12Z",
          "html_url": "https://github.com/octocat/Hello-World/pull/1347",
          "draft": false,
          "merged": false,
          "mergeable": true,
          "merged_by": {
            "login": "octocat",
            "id": 1,
            "avatar_url": "https://github.com/images/error/octocat_happy.gif",
            "html_url": "https://github.com/octocat"
          },
          "comments": 10,
          "commits": 3,
          "additions": 100,
          "deletions": 3,
          "changed_files": 5,
          "head": {
            "label": "octocat:new-topic",
            "ref": "new-topic",
            "sha": "6dcb09b5b57875f334f61aebed695e2e4193db5e",
            "user": {
              "login": "octocat",
              "id": 1,
              "avatar_url": "https://github.com/images/error/octocat_happy.gif",
              "html_url": "https://github.com/octocat"
            }
          },
          "base": {
            "label": "octocat:master",
            "ref": "master",
            "sha": "6dcb09b5b57875f334f61aebed695e2e4193db5e",
            "user": {
              "login": "octocat",
              "id": 1,
              "avatar_url": "https://github.com/images/error/octocat_happy.gif",
              "html_url": "https://github.com/octocat"
            }
          }
        }
        """;

    /// <summary>Unmodified Get-an-issue example from GitHub REST docs (trimmed).</summary>
    public const string OfficialIssue = """
        {
          "id": 1,
          "number": 1347,
          "title": "Found a bug",
          "body": "I'm having a problem with this.",
          "state": "open",
          "html_url": "https://github.com/octocat/Hello-World/issues/1347",
          "created_at": "2011-04-22T13:33:48Z",
          "updated_at": "2011-04-22T13:33:48Z",
          "comments": 0,
          "user": {
            "login": "octocat",
            "id": 1,
            "avatar_url": "https://github.com/images/error/octocat_happy.gif",
            "html_url": "https://github.com/octocat"
          },
          "labels": [],
          "assignees": [],
          "pull_request": {
            "url": "https://api.github.com/repos/octocat/Hello-World/pulls/1347",
            "html_url": "https://github.com/octocat/Hello-World/pull/1347",
            "diff_url": "https://github.com/octocat/Hello-World/pull/1347.diff",
            "patch_url": "https://github.com/octocat/Hello-World/pull/1347.patch"
          }
        }
        """;

    /// <summary>Unmodified issue-comment example from GitHub REST docs (trimmed).</summary>
    public const string OfficialComment = """
        {
          "id": 1,
          "body": "Me too",
          "user": {
            "login": "octocat",
            "id": 1,
            "avatar_url": "https://github.com/images/error/octocat_happy.gif",
            "html_url": "https://github.com/octocat"
          },
          "created_at": "2011-04-14T16:00:49Z",
          "updated_at": "2011-04-14T16:00:49Z",
          "html_url": "https://github.com/octocat/Hello-World/issues/1347#issuecomment-1"
        }
        """;

    private static string Bool(bool value) => value ? "true" : "false";
}
