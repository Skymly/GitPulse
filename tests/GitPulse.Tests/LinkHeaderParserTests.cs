using System.Net.Http.Headers;
using GitPulse.Core.Http;
using Xunit;

namespace GitPulse.Tests;

public class LinkHeaderParserTests
{
    [Fact]
    public void GetNextUrl_NullHeaders_ReturnsNull()
    {
        Assert.Null(LinkHeaderParser.GetNextUrl(null));
    }

    [Fact]
    public void GetNextUrl_NoLinkHeader_ReturnsNull()
    {
        var headers = new HttpResponseMessage().Headers;

        Assert.Null(LinkHeaderParser.GetNextUrl(headers));
    }

    [Fact]
    public void GetNextUrl_WithNextRel_ReturnsUrl()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add("Link",
            "<https://api.github.com/repos/owner/repo/issues?page=2>; rel=\"next\", " +
            "<https://api.github.com/repos/owner/repo/issues?page=5>; rel=\"last\"");

        var next = LinkHeaderParser.GetNextUrl(response.Headers);

        Assert.Equal("https://api.github.com/repos/owner/repo/issues?page=2", next);
    }

    [Fact]
    public void GetNextUrl_NoNextRel_ReturnsNull()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add("Link",
            "<https://api.github.com/repos/owner/repo/issues?page=1>; rel=\"prev\", " +
            "<https://api.github.com/repos/owner/repo/issues?page=1>; rel=\"first\"");

        Assert.Null(LinkHeaderParser.GetNextUrl(response.Headers));
    }

    [Fact]
    public void GetNextPageNumber_WithNextUrl_ReturnsPageNumber()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add("Link",
            "<https://api.github.com/repos/owner/repo/issues?page=3>; rel=\"next\"");

        Assert.Equal(3, LinkHeaderParser.GetNextPageNumber(response.Headers));
    }

    [Fact]
    public void GetNextPageNumber_NoNextUrl_ReturnsNull()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add("Link",
            "<https://api.github.com/repos/owner/repo/issues?page=1>; rel=\"first\"");

        Assert.Null(LinkHeaderParser.GetNextPageNumber(response.Headers));
    }
}
