using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
using GitPulse.Core.Models;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class SearchViewModelTests
{
    private const string NextLink =
        "<https://api.github.com/search/repositories?q=gitpulse&page=2>; rel=\"next\"";

    [Fact]
    public async Task Search_WithoutToken_SetsAuthenticationErrorWithoutRequest()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(RepositoryResults("unused"))));
        using var vm = new SearchViewModel(
            new RecordingClientFactory(handler, token: null));
        vm.Query.Value = "gitpulse";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public async Task Search_WithShortQuery_DoesNotSendRequest(string query)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(RepositoryResults("unused"))));
        using var vm = CreateViewModel(handler);
        vm.Query.Value = query;

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(handler.Requests);
        Assert.Contains("3 characters", vm.ErrorMessage.Value);
    }

    [Fact]
    public async Task QueryChange_RequiresExplicitSearchCommand()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(RepositoryResults("GitPulse"))));
        using var vm = CreateViewModel(handler);

        vm.Query.Value = "gitpulse";
        Assert.Empty(handler.Requests);

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Single(handler.Requests);
        Assert.Single(vm.Repositories);
    }

    [Theory]
    [InlineData(SearchType.Repositories, "/search/repositories", "")]
    [InlineData(SearchType.Issues, "/search/issues", "is%3Aissue")]
    [InlineData(SearchType.PullRequests, "/search/issues", "is%3Apr")]
    [InlineData(SearchType.Code, "/search/code", "")]
    public async Task Search_UsesEndpointAndQualifierForSelectedType(
        SearchType type,
        string expectedPath,
        string expectedQualifier)
    {
        var handler = new RecordingHandler((request, _, _) =>
        {
            var body = type switch
            {
                SearchType.Repositories => RepositoryResults("GitPulse"),
                SearchType.Issues or SearchType.PullRequests => IssueResults("Search item"),
                SearchType.Code => CodeResults("SearchViewModel.cs"),
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
            return Task.FromResult(JsonResponse(body));
        });
        using var vm = CreateViewModel(handler);
        vm.SelectedType.Value = type;
        vm.Query.Value = "search term";

        await vm.SearchCommand.ExecuteAsync(null);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal(expectedPath, uri.AbsolutePath);
        Assert.Contains("q=search%20term", uri.Query, StringComparison.OrdinalIgnoreCase);
        if (expectedQualifier.Length > 0)
            Assert.Contains(expectedQualifier, uri.Query, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, ResultCount(vm, type));
    }

    [Fact]
    public async Task Search_EncodesSpecialCharactersInQuery()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(RepositoryResults("GitPulse"))));
        using var vm = CreateViewModel(handler);
        vm.Query.Value = "owner/repo C#";

        await vm.SearchCommand.ExecuteAsync(null);

        var query = Assert.Single(handler.Requests).Query;
        Assert.Contains("owner%2Frepo%20C%23", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadMore_AppendsNextPageAndStopsAtLinkEnd()
    {
        var handler = new RecordingHandler((request, _, _) =>
        {
            var isSecondPage = request.RequestUri?.Query.Contains("page=2") == true;
            return Task.FromResult(JsonResponse(
                RepositoryResults(isSecondPage ? "Second" : "First", totalCount: 2),
                isSecondPage ? null : NextLink));
        });
        using var vm = CreateViewModel(handler);
        vm.Query.Value = "gitpulse";

        await vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.CanLoadMore.Value);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["First", "Second"], vm.Repositories.Select(repo => repo.Name));
        Assert.False(vm.CanLoadMore.Value);
        Assert.Contains("page=2", handler.Requests.Last().Query);
    }

    [Fact]
    public async Task SwitchingType_CancelsActiveRequestWithoutStartingAnother()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, _, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(RepositoryResults("unused"));
        });
        using var vm = CreateViewModel(handler);
        vm.Query.Value = "first query";

        var search = vm.SearchCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        vm.SelectedType.Value = SearchType.Code;
        await search;

        Assert.Single(handler.Requests);
        Assert.False(vm.IsLoading.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.True(vm.IsCodeSelected.Value);
    }

    [Fact]
    public async Task ConsecutiveSearch_DiscardsStaleResponse()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, requestNumber, _) =>
        {
            if (requestNumber == 1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
                return JsonResponse(RepositoryResults("Stale"));
            }

            return JsonResponse(RepositoryResults("Current"));
        });
        using var vm = CreateViewModel(handler);
        vm.Query.Value = "first query";

        var firstSearch = vm.SearchCommand.ExecuteAsync(null);
        await firstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        vm.Query.Value = "second query";
        var secondSearch = vm.SearchCommand.ExecuteAsync(null);
        await secondSearch;

        releaseFirst.SetResult();
        await firstSearch;

        var repo = Assert.Single(vm.Repositories);
        Assert.Equal("Current", repo.Name);
    }

    [Fact]
    public async Task Search_WithNoItems_SetsEmptyState()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(EmptyResults())));
        using var vm = CreateViewModel(handler);
        vm.Query.Value = "nothing";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(vm.HasSearched.Value);
        Assert.True(vm.IsEmpty.Value);
        Assert.Equal(0, vm.TotalCount.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "rate limit")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "syntax")]
    [InlineData(HttpStatusCode.InternalServerError, "Search failed")]
    public async Task Search_HttpFailure_ShowsSpecificMessage(
        HttpStatusCode statusCode,
        string expectedMessage)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            }));
        using var vm = CreateViewModel(handler);
        vm.Query.Value = "failing query";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Contains(expectedMessage, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsLoading.Value);
    }

    [Fact]
    public async Task DefaultHub_DoesNotLoadAnyInbox()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("should-not-load"))));
        using var vm = CreateViewModel(handler);

        Assert.Equal(SearchViewModel.SearchHub, vm.SelectedHub.Value);
        Assert.True(vm.IsSearchHub.Value);
        Assert.False(vm.IsReviewRequestedHub.Value);
        Assert.False(vm.IsAssignedHub.Value);
        Assert.False(vm.IsMentionsHub.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(vm.ReviewRequested);
        Assert.Empty(vm.Assigned);
        Assert.Empty(vm.Mentions);
    }

    [Theory]
    [MemberData(nameof(InboxHubs))]
    public async Task SelectHub_Inbox_UsesCannedQueryWithoutThreeCharGate(string hub)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("Inbox item", totalCount: 4))));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(hub);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("/search/issues", uri.AbsolutePath);
        Assert.Contains(
            "q=" + Uri.EscapeDataString(InboxQuery(hub)),
            uri.Query,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InboxExtraQualifier(hub), uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(hub, vm.SelectedHub.Value);
        Assert.True(IsInboxSelected(vm, hub));
        Assert.False(vm.IsIssuesSelected.Value);
        Assert.False(vm.IsPullRequestsSelected.Value);
        var item = Assert.Single(InboxItems(vm, hub));
        Assert.Equal("Inbox item", item.Title);
        Assert.Equal(4, vm.TotalCount.Value);
        Assert.True(vm.HasSearched.Value);
        Assert.False(vm.IsEmpty.Value);
    }

    [Theory]
    [MemberData(nameof(InboxHubs))]
    public async Task SelectHub_Inbox_WithoutToken_SetsAuthenticationErrorWithoutRequest(string hub)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("unused"))));
        using var vm = new SearchViewModel(new RecordingClientFactory(handler, token: null));

        await vm.SelectHubCommand.ExecuteAsync(hub);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(InboxItems(vm, hub));
        Assert.Equal(hub, vm.SelectedHub.Value);
    }

    [Theory]
    [MemberData(nameof(InboxHubs))]
    public async Task LoadMore_Inbox_AppendsNextPage(string hub)
    {
        var nextLink =
            "<https://api.github.com/search/issues?q=inbox&page=2>; rel=\"next\"";
        var handler = new RecordingHandler((request, _, _) =>
        {
            var isSecondPage = request.RequestUri?.Query.Contains("page=2") == true;
            return Task.FromResult(JsonResponse(
                IssueResults(isSecondPage ? "Second" : "First", totalCount: 2),
                isSecondPage ? null : nextLink));
        });
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(hub);
        Assert.True(vm.CanLoadMore.Value);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["First", "Second"], InboxItems(vm, hub).Select(item => item.Title));
        Assert.False(vm.CanLoadMore.Value);
        Assert.Contains("page=2", handler.Requests.Last().Query);
    }

    [Fact]
    public async Task SelectHub_DoesNotMixTypedSearchAndInbox()
    {
        var handler = new RecordingHandler((request, _, _) =>
        {
            var q = request.RequestUri?.Query ?? string.Empty;
            var isInbox = q.Contains("review-requested", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(JsonResponse(
                IssueResults(isInbox ? "Inbox PR" : "Typed PR")));
        });
        using var vm = CreateViewModel(handler);
        vm.SelectedType.Value = SearchType.PullRequests;
        vm.Query.Value = "typed query";

        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Equal("Typed PR", Assert.Single(vm.PullRequests).Title);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);
        Assert.Equal("Inbox PR", Assert.Single(vm.ReviewRequested).Title);
        Assert.Equal("Typed PR", Assert.Single(vm.PullRequests).Title);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.SearchHub);
        Assert.True(vm.IsSearchHub.Value);
        Assert.Equal("Typed PR", Assert.Single(vm.PullRequests).Title);
        Assert.Equal("Inbox PR", Assert.Single(vm.ReviewRequested).Title);
    }

    [Theory]
    [MemberData(nameof(InboxHubs))]
    public async Task SelectHub_EmptyInbox_IsQuiet(string hub)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(EmptyResults())));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(hub);

        Assert.True(vm.HasSearched.Value);
        Assert.True(vm.IsEmpty.Value);
        Assert.Empty(InboxItems(vm, hub));
        Assert.Empty(vm.ErrorMessage.Value);
    }

    [Theory]
    [MemberData(nameof(InboxHttpFailures))]
    public async Task SelectHub_InboxHttpFailure_ShowsSpecificMessage(
        string hub,
        HttpStatusCode statusCode,
        string expectedMessage)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            }));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(hub);

        Assert.Contains(expectedMessage, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(hub, vm.SelectedHub.Value);
        Assert.False(vm.IsLoading.Value);
    }

    [Fact]
    public async Task SelectHub_DoesNotMixAssignedAndReviewInbox()
    {
        var handler = new RecordingHandler((request, _, _) =>
        {
            var q = request.RequestUri?.Query ?? string.Empty;
            var title = q.Contains("assignee", StringComparison.OrdinalIgnoreCase)
                ? "Assigned item"
                : "Inbox PR";
            return Task.FromResult(JsonResponse(IssueResults(title)));
        });
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);
        Assert.Equal("Inbox PR", Assert.Single(vm.ReviewRequested).Title);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);
        Assert.Equal("Assigned item", Assert.Single(vm.Assigned).Title);
        Assert.Equal("Inbox PR", Assert.Single(vm.ReviewRequested).Title);
        Assert.True(vm.IsAssignedHub.Value);
        Assert.False(vm.IsReviewRequestedSelected.Value);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);
        Assert.True(vm.IsReviewRequestedHub.Value);
        Assert.Equal("Inbox PR", Assert.Single(vm.ReviewRequested).Title);
        Assert.Equal("Assigned item", Assert.Single(vm.Assigned).Title);
    }

    [Fact]
    public async Task SelectHub_DoesNotMixMentionsAndAssigned()
    {
        var handler = new RecordingHandler((request, _, _) =>
        {
            var q = request.RequestUri?.Query ?? string.Empty;
            var title = q.Contains("mentions", StringComparison.OrdinalIgnoreCase)
                ? "Mentioned item"
                : "Assigned item";
            return Task.FromResult(JsonResponse(IssueResults(title)));
        });
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);
        Assert.Equal("Assigned item", Assert.Single(vm.Assigned).Title);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.MentionsHub);
        Assert.Equal("Mentioned item", Assert.Single(vm.Mentions).Title);
        Assert.Equal("Assigned item", Assert.Single(vm.Assigned).Title);
        Assert.True(vm.IsMentionsHub.Value);
        Assert.False(vm.IsAssignedSelected.Value);
    }

    [Fact]
    public async Task Search_StillRequiresThreeCharactersOnSearchHub()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(RepositoryResults("unused"))));
        using var vm = CreateViewModel(handler);
        vm.Query.Value = "ab";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(handler.Requests);
        Assert.Contains("3 characters", vm.ErrorMessage.Value);
    }

    public static TheoryData<string> InboxHubs => new()
    {
        SearchViewModel.ReviewRequestedHub,
        SearchViewModel.AssignedHub,
        SearchViewModel.MentionsHub,
    };

    public static TheoryData<string, HttpStatusCode, string> InboxHttpFailures
    {
        get
        {
            var data = new TheoryData<string, HttpStatusCode, string>();
            foreach (var hub in new[]
                     {
                         SearchViewModel.ReviewRequestedHub,
                         SearchViewModel.AssignedHub,
                         SearchViewModel.MentionsHub,
                     })
            {
                data.Add(hub, HttpStatusCode.Forbidden, "rate limit");
                data.Add(hub, HttpStatusCode.UnprocessableEntity, "syntax");
                data.Add(hub, HttpStatusCode.InternalServerError, "Load failed");
            }

            return data;
        }
    }

    private static string InboxQuery(string hub)
    {
        return hub switch
        {
            SearchViewModel.ReviewRequestedHub => SearchViewModel.ReviewRequestedQuery,
            SearchViewModel.AssignedHub => SearchViewModel.AssignedQuery,
            SearchViewModel.MentionsHub => SearchViewModel.MentionsQuery,
            _ => throw new ArgumentOutOfRangeException(nameof(hub), hub, null),
        };
    }

    private static string InboxExtraQualifier(string hub)
    {
        return hub == SearchViewModel.ReviewRequestedHub
            ? "false%20is%3Apr"
            : "false%20is%3Aissue";
    }

    private static IReadOnlyList<SearchIssueItem> InboxItems(
        SearchViewModel vm,
        string hub)
    {
        return hub switch
        {
            SearchViewModel.ReviewRequestedHub => vm.ReviewRequested,
            SearchViewModel.AssignedHub => vm.Assigned,
            SearchViewModel.MentionsHub => vm.Mentions,
            _ => throw new ArgumentOutOfRangeException(nameof(hub), hub, null),
        };
    }

    private static bool IsInboxSelected(SearchViewModel vm, string hub)
    {
        return hub switch
        {
            SearchViewModel.ReviewRequestedHub => vm.IsReviewRequestedSelected.Value,
            SearchViewModel.AssignedHub => vm.IsAssignedSelected.Value,
            SearchViewModel.MentionsHub => vm.IsMentionsSelected.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(hub), hub, null),
        };
    }

    private static SearchViewModel CreateViewModel(RecordingHandler handler)
    {
        return new SearchViewModel(new RecordingClientFactory(handler));
    }

    private static int ResultCount(SearchViewModel vm, SearchType type)
    {
        return type switch
        {
            SearchType.Repositories => vm.Repositories.Count,
            SearchType.Issues => vm.Issues.Count,
            SearchType.PullRequests => vm.PullRequests.Count,
            SearchType.Code => vm.CodeResults.Count,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    private static HttpResponseMessage JsonResponse(string body, string? link = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (link is not null)
            response.Headers.Add("Link", link);
        return response;
    }

    private static string RepositoryResults(string name, int totalCount = 1)
    {
        return $$"""
            {
              "total_count": {{totalCount}},
              "incomplete_results": false,
              "items": [
                {
                  "id": 1,
                  "name": "{{name}}",
                  "full_name": "owner/{{name}}",
                  "html_url": "https://github.com/owner/{{name}}"
                }
              ]
            }
            """;
    }

    private static string IssueResults(string title, int totalCount = 1)
    {
        return $$"""
            {
              "total_count": {{totalCount}},
              "incomplete_results": false,
              "items": [
                {
                  "id": 2,
                  "number": 3,
                  "title": "{{title}}",
                  "state": "open",
                  "repository_url": "https://api.github.com/repos/owner/repo"
                }
              ]
            }
            """;
    }

    private static string CodeResults(string name)
    {
        return $$"""
            {
              "total_count": 1,
              "incomplete_results": false,
              "items": [
                {
                  "name": "{{name}}",
                  "path": "src/{{name}}",
                  "sha": "abc123",
                  "repository": {
                    "id": 1,
                    "name": "repo",
                    "full_name": "owner/repo"
                  }
                }
              ]
            }
            """;
    }

    private static string EmptyResults()
    {
        return """
            {
              "total_count": 0,
              "incomplete_results": false,
              "items": []
            }
            """;
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        private int _requestCount;

        public ConcurrentQueue<Uri> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Enqueue(request.RequestUri!);
            var requestNumber = Interlocked.Increment(ref _requestCount);
            var response = await responder(request, requestNumber, cancellationToken);
            response.RequestMessage ??= request;
            return response;
        }
    }

    private sealed class RecordingClientFactory(
        RecordingHandler handler,
        string? token = "test_token")
        : IGitHubClientFactory
    {
        public Task<HttpClient> CreateClientAsync(CancellationToken ct = default)
        {
            return Task.FromResult(BuildClient(handler));
        }

        public Task<(HttpClient Client, GitHubQueryHandler QueryHandler)>
            CreatePagedClientAsync(CancellationToken ct = default)
        {
            var queryHandler = new GitHubQueryHandler(handler);
            return Task.FromResult((BuildClient(queryHandler), queryHandler));
        }

        public Task<PagedGitHubSession> CreatePagedSessionAsync(CancellationToken ct = default)
        {
            var queryHandler = new GitHubQueryHandler(handler);
            return Task.FromResult(new PagedGitHubSession(BuildClient(queryHandler), queryHandler));
        }

        private HttpClient BuildClient(HttpMessageHandler messageHandler)
        {
            var client = new HttpClient(messageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };

            if (token is not null)
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
    }
}



