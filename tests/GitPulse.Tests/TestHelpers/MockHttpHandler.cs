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

    /// <summary>Shorthand for a status-only response (optional JSON body).</summary>
    public MockHttpHandler When(
        string pathSuffix,
        HttpStatusCode statusCode,
        string jsonBody = "")
        => When(pathSuffix, _ => new MockResponse(jsonBody, LinkHeader: null, StatusCode: statusCode));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        // Normalize trailing slash so "/contents/" matches "/contents".
        if (path.EndsWith('/') && path.Length > 1)
            path = path[..^1];

        string? matchedSuffix = null;
        Func<HttpRequestMessage, MockResponse>? matchedResponder = null;
        foreach (var (suffix, responder) in _routes)
        {
            if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Prefer the longest suffix so /runs/101/rerun does not match /runs/101.
            if (matchedSuffix is null || suffix.Length > matchedSuffix.Length)
            {
                matchedSuffix = suffix;
                matchedResponder = responder;
            }
        }

        if (matchedResponder is not null)
        {
            var mock = matchedResponder(request);
            var response = new HttpResponseMessage(mock.StatusCode)
            {
                Content = new StringContent(
                    mock.Body,
                    Encoding.UTF8,
                    string.IsNullOrEmpty(mock.Body) ? "text/plain" : "application/json"),
            };
            if (!string.IsNullOrEmpty(mock.LinkHeader))
                response.Headers.Add("Link", mock.LinkHeader);
            return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No mock for {path}", Encoding.UTF8, "text/plain"),
        });
    }
}

/// <summary>Canned response payload for <see cref="MockHttpHandler"/>.</summary>
public sealed record MockResponse(
    string Body,
    string? LinkHeader = null,
    HttpStatusCode StatusCode = HttpStatusCode.OK);
