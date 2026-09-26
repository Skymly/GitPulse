using GitPulse.Core.Http;
using GitPulse.GitHubApi;
using GitPulse.Tests.TestHelpers;
using Observables.RestAPI;
using R3;
using Xunit;

namespace GitPulse.Tests;

public class ListNotificationsApiTests
{
    [Fact]
    public async Task ListNotifications_ExposesLinkRelNextOnApiResponse()
    {
        var handler = new MockHttpHandler()
            .When(
                "/notifications",
                "[{\"id\":\"1\",\"unread\":true,\"reason\":\"mention\"," +
                "\"updated_at\":\"2025-01-01T00:00:00Z\"," +
                "\"url\":\"https://api.github.com/notifications/threads/1\"," +
                "\"subject\":{\"title\":\"One\",\"type\":\"Issue\"," +
                "\"url\":\"https://api.github.com/repos/o/r/issues/1\"}," +
                "\"repository\":{\"id\":1,\"name\":\"r\",\"full_name\":\"o/r\"," +
                "\"html_url\":\"https://github.com/o/r\"}}]",
                "<https://api.github.com/notifications?page=2>; rel=\"next\"");
        var factory = new FakeGitHubClientFactory(handler);
        using var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);
        var api = RestService.For<IGitHubReposApi>(client);

        using var response = await api.ListNotifications().FirstAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("1", Assert.Single(response.Content ?? []).Id);
        Assert.Equal(
            "https://api.github.com/notifications?page=2",
            LinkHeaderParser.GetNextUrl(response.Headers));
    }
}
