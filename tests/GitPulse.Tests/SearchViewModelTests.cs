using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
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
    public async Task DefaultHub_DoesNotLoadReviewRequested()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("should-not-load"))));
        using var vm = CreateViewModel(handler);

        Assert.Equal(SearchViewModel.SearchHub, vm.SelectedHub.Value);
        Assert.True(vm.IsSearchHub.Value);
        Assert.False(vm.IsReviewRequestedHub.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(vm.ReviewRequested);
    }

    [Fact]
    public async Task SelectHub_ReviewRequested_UsesCannedQueryWithoutThreeCharGate()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("Needs review", totalCount: 4))));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("/search/issues", uri.AbsolutePath);
        Assert.Contains(
            "q=" + Uri.EscapeDataString(SearchViewModel.ReviewRequestedQuery),
            uri.Query,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("false%20is%3Apr", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SearchViewModel.ReviewRequestedHub, vm.SelectedHub.Value);
        Assert.True(vm.IsReviewRequestedSelected.Value);
        Assert.False(vm.IsPullRequestsSelected.Value);
        var item = Assert.Single(vm.ReviewRequested);
        Assert.Equal("Needs review", item.Title);
        Assert.Equal(4, vm.TotalCount.Value);
        Assert.True(vm.HasSearched.Value);
        Assert.False(vm.IsEmpty.Value);
    }

    [Fact]
    public async Task SelectHub_ReviewRequested_WithoutToken_SetsAuthenticationErrorWithoutRequest()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("unused"))));
        using var vm = new SearchViewModel(new RecordingClientFactory(handler, token: null));

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(vm.ReviewRequested);
        Assert.Equal(SearchViewModel.ReviewRequestedHub, vm.SelectedHub.Value);
    }

    [Fact]
    public async Task LoadMore_ReviewRequested_AppendsNextPage()
    {
        var nextLink =
            "<https://api.github.com/search/issues?q=review&page=2>; rel=\"next\"";
        var handler = new RecordingHandler((request, _, _) =>
        {
            var isSecondPage = request.RequestUri?.Query.Contains("page=2") == true;
            return Task.FromResult(JsonResponse(
                IssueResults(isSecondPage ? "Second" : "First", totalCount: 2),
                isSecondPage ? null : nextLink));
        });
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);
        Assert.True(vm.CanLoadMore.Value);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["First", "Second"], vm.ReviewRequested.Select(item => item.Title));
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

    [Fact]
    public async Task SelectHub_EmptyReviewRequested_IsQuiet()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(EmptyResults())));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);

        Assert.True(vm.HasSearched.Value);
        Assert.True(vm.IsEmpty.Value);
        Assert.Empty(vm.ReviewRequested);
        Assert.Empty(vm.ErrorMessage.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "rate limit")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "syntax")]
    [InlineData(HttpStatusCode.InternalServerError, "Load failed")]
    public async Task SelectHub_ReviewRequestedHttpFailure_ShowsSpecificMessage(
        HttpStatusCode statusCode,
        string expectedMessage)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            }));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);

        Assert.Contains(expectedMessage, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SearchViewModel.ReviewRequestedHub, vm.SelectedHub.Value);
        Assert.False(vm.IsLoading.Value);
    }

    [Fact]
    public async Task DefaultHub_DoesNotLoadAssigned()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("should-not-load"))));
        using var vm = CreateViewModel(handler);

        Assert.Equal(SearchViewModel.SearchHub, vm.SelectedHub.Value);
        Assert.False(vm.IsAssignedHub.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(vm.Assigned);
    }

    [Fact]
    public async Task SelectHub_Assigned_UsesCannedQueryWithoutThreeCharGate()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("Assigned to me", totalCount: 5))));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("/search/issues", uri.AbsolutePath);
        Assert.Contains(
            "q=" + Uri.EscapeDataString(SearchViewModel.AssignedQuery),
            uri.Query,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("false%20is%3Aissue", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SearchViewModel.AssignedHub, vm.SelectedHub.Value);
        Assert.True(vm.IsAssignedSelected.Value);
        Assert.False(vm.IsReviewRequestedSelected.Value);
        Assert.False(vm.IsIssuesSelected.Value);
        var item = Assert.Single(vm.Assigned);
        Assert.Equal("Assigned to me", item.Title);
        Assert.Equal(5, vm.TotalCount.Value);
        Assert.True(vm.HasSearched.Value);
        Assert.False(vm.IsEmpty.Value);
    }

    [Fact]
    public async Task SelectHub_Assigned_WithoutToken_SetsAuthenticationErrorWithoutRequest()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("unused"))));
        using var vm = new SearchViewModel(new RecordingClientFactory(handler, token: null));

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(vm.Assigned);
        Assert.Equal(SearchViewModel.AssignedHub, vm.SelectedHub.Value);
    }

    [Fact]
    public async Task LoadMore_Assigned_AppendsNextPage()
    {
        var nextLink =
            "<https://api.github.com/search/issues?q=assigned&page=2>; rel=\"next\"";
        var handler = new RecordingHandler((request, _, _) =>
        {
            var isSecondPage = request.RequestUri?.Query.Contains("page=2") == true;
            return Task.FromResult(JsonResponse(
                IssueResults(isSecondPage ? "Second" : "First", totalCount: 2),
                isSecondPage ? null : nextLink));
        });
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);
        Assert.True(vm.CanLoadMore.Value);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["First", "Second"], vm.Assigned.Select(item => item.Title));
        Assert.False(vm.CanLoadMore.Value);
        Assert.Contains("page=2", handler.Requests.Last().Query);
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
    public async Task SelectHub_EmptyAssigned_IsQuiet()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(EmptyResults())));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);

        Assert.True(vm.HasSearched.Value);
        Assert.True(vm.IsEmpty.Value);
        Assert.Empty(vm.Assigned);
        Assert.Empty(vm.ErrorMessage.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "rate limit")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "syntax")]
    [InlineData(HttpStatusCode.InternalServerError, "Load failed")]
    public async Task SelectHub_AssignedHttpFailure_ShowsSpecificMessage(
        HttpStatusCode statusCode,
        string expectedMessage)
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            }));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);

        Assert.Contains(expectedMessage, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SearchViewModel.AssignedHub, vm.SelectedHub.Value);
        Assert.False(vm.IsLoading.Value);
    }

    [Fact]
    public async Task DefaultHub_DoesNotLoadMentions()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("should-not-load"))));
        using var vm = CreateViewModel(handler);

        Assert.False(vm.IsMentionsHub.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(vm.Mentions);
    }

    [Fact]
    public async Task SelectHub_Mentions_UsesCannedQueryWithoutThreeCharGate()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("Mentioned me", totalCount: 3))));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.MentionsHub);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("/search/issues", uri.AbsolutePath);
        Assert.Contains(
            "q=" + Uri.EscapeDataString(SearchViewModel.MentionsQuery),
            uri.Query,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("false%20is%3Aissue", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SearchViewModel.MentionsHub, vm.SelectedHub.Value);
        Assert.True(vm.IsMentionsSelected.Value);
        Assert.False(vm.IsAssignedSelected.Value);
        var item = Assert.Single(vm.Mentions);
        Assert.Equal("Mentioned me", item.Title);
        Assert.Equal(3, vm.TotalCount.Value);
        Assert.True(vm.HasSearched.Value);
    }

    [Fact]
    public async Task SelectHub_Mentions_WithoutToken_SetsAuthenticationErrorWithoutRequest()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(IssueResults("unused"))));
        using var vm = new SearchViewModel(new RecordingClientFactory(handler, token: null));

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.MentionsHub);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(handler.Requests);
        Assert.Empty(vm.Mentions);
    }

    [Fact]
    public async Task LoadMore_Mentions_AppendsNextPage()
    {
        var nextLink =
            "<https://api.github.com/search/issues?q=mentions&page=2>; rel=\"next\"";
        var handler = new RecordingHandler((request, _, _) =>
        {
            var isSecondPage = request.RequestUri?.Query.Contains("page=2") == true;
            return Task.FromResult(JsonResponse(
                IssueResults(isSecondPage ? "Second" : "First", totalCount: 2),
                isSecondPage ? null : nextLink));
        });
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.MentionsHub);
        Assert.True(vm.CanLoadMore.Value);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["First", "Second"], vm.Mentions.Select(item => item.Title));
        Assert.Contains("page=2", handler.Requests.Last().Query);
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
    public async Task SelectHub_EmptyMentions_IsQuiet()
    {
        var handler = new RecordingHandler((_, _, _) =>
            Task.FromResult(JsonResponse(EmptyResults())));
        using var vm = CreateViewModel(handler);

        await vm.SelectHubCommand.ExecuteAsync(SearchViewModel.MentionsHub);

        Assert.True(vm.HasSearched.Value);
        Assert.True(vm.IsEmpty.Value);
        Assert.Empty(vm.Mentions);
        Assert.Empty(vm.ErrorMessage.Value);
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



