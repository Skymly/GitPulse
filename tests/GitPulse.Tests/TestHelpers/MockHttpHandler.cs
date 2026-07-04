using System.Net;
using System.Text;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// A minimal <see cref="HttpMessageHandler"/> that returns canned JSON
/// responses keyed by request URI path. Enables ViewModel tests to exercise
/// the full <c>RestService.For&lt;T&gt;</c> → HTTP → deserialize pipeline
/// without a real network.
/// </summary>
public sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, MockResponse>> _routes = new();

    /// <summary>
    /// Register a canned response (JSON body + optional Link header) for
    /// requests whose absolute path ends with <paramref name="pathSuffix"/>.
    /// </summary>
    public MockHttpHandler When(string pathSuffix, Func<HttpRequestMessage, MockResponse> respond)
    {
        _routes[pathSuffix] = respond;
        return this;
    }

    /// <summary>Shorthand for a constant JSON body with no Link header.</summary>
    public MockHttpHandler When(string pathSuffix, string jsonBody)
        => When(pathSuffix, _ => new MockResponse(jsonBody));

    /// <summary>Shorthand for a JSON body with a Link header.</summary>
    public MockHttpHandler When(string pathSuffix, string jsonBody, string? linkHeader)
        => When(pathSuffix, _ => new MockResponse(jsonBody, linkHeader));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        // Normalize trailing slash so "/contents/" matches "/contents".
        if (path.EndsWith('/') && path.Length > 1)
            path = path[..^1];

        foreach (var (suffix, responder) in _routes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var mock = responder(request);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(mock.Body, Encoding.UTF8, "application/json"),
                };
                if (!string.IsNullOrEmpty(mock.LinkHeader))
                    response.Headers.Add("Link", mock.LinkHeader);
                return Task.FromResult(response);
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No mock for {path}", Encoding.UTF8, "text/plain"),
        });
    }
}

/// <summary>Canned response payload for <see cref="MockHttpHandler"/>.</summary>
public sealed record MockResponse(string Body, string? LinkHeader = null);
