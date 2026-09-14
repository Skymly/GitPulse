using System.Net;
using System.Text;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// A minimal <see cref="HttpMessageHandler"/> that returns canned JSON
/// responses keyed by request URI path (and optional HTTP method). Enables
/// ViewModel tests to exercise the full <c>RestService.For&lt;T&gt;</c> → HTTP →
/// deserialize pipeline without a real network.
/// </summary>
public sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly List<MockRoute> _routes = [];

    /// <summary>
    /// Register a canned response for requests whose absolute path ends with
    /// <paramref name="pathSuffix"/>. Matches every HTTP method.
    /// </summary>
    public MockHttpHandler When(string pathSuffix, Func<HttpRequestMessage, MockResponse> respond)
        => Add(pathSuffix, method: null, respond);

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

    /// <summary>
    /// Register a canned response that matches only <paramref name="method"/>
    /// plus the path suffix. A GET route will not answer PATCH/POST.
    /// </summary>
    public MockHttpHandler When(
        HttpMethod method,
        string pathSuffix,
        Func<HttpRequestMessage, MockResponse> respond)
        => Add(pathSuffix, method, respond);

    /// <summary>Shorthand for a method-specific JSON body.</summary>
    public MockHttpHandler When(HttpMethod method, string pathSuffix, string jsonBody)
        => When(method, pathSuffix, _ => new MockResponse(jsonBody));

    /// <summary>Shorthand for a method-specific status-only response.</summary>
    public MockHttpHandler When(
        HttpMethod method,
        string pathSuffix,
        HttpStatusCode statusCode,
        string jsonBody = "")
        => When(method, pathSuffix, _ => new MockResponse(jsonBody, LinkHeader: null, StatusCode: statusCode));

    private MockHttpHandler Add(
        string pathSuffix,
        HttpMethod? method,
        Func<HttpRequestMessage, MockResponse> respond)
    {
        _routes.Add(new MockRoute(pathSuffix, method, respond));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        // Normalize trailing slash so "/contents/" matches "/contents".
        if (path.EndsWith('/') && path.Length > 1)
            path = path[..^1];

        MockRoute? matched = null;
        foreach (var route in _routes)
        {
            if (!path.EndsWith(route.Suffix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (route.Method is not null && route.Method != request.Method)
                continue;

            if (matched is null
                || route.Suffix.Length > matched.Suffix.Length
                || (route.Suffix.Length == matched.Suffix.Length
                    && route.Method is not null
                    && matched.Method is null))
            {
                matched = route;
            }
            else if (route.Suffix.Length == matched.Suffix.Length
                && (route.Method is not null) == (matched.Method is not null))
            {
                // Same specificity: later registration wins (old dictionary overwrite).
                matched = route;
            }
        }

        if (matched is not null)
        {
            var mock = matched.Respond(request);
            if (mock.Gate is not null)
                await mock.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            var response = new HttpResponseMessage(mock.StatusCode)
            {
                Content = new StringContent(
                    mock.Body,
                    Encoding.UTF8,
                    string.IsNullOrEmpty(mock.Body) ? "text/plain" : "application/json"),
            };
            if (!string.IsNullOrEmpty(mock.LinkHeader))
                response.Headers.Add("Link", mock.LinkHeader);
            if (!string.IsNullOrEmpty(mock.RetryAfter))
                response.Headers.TryAddWithoutValidation("Retry-After", mock.RetryAfter);
            if (mock.AttachRequest)
                response.RequestMessage = request;
            return response;
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No mock for {request.Method} {path}", Encoding.UTF8, "text/plain"),
        };
    }

    private sealed record MockRoute(
        string Suffix,
        HttpMethod? Method,
        Func<HttpRequestMessage, MockResponse> Respond);
}

/// <summary>Canned response payload for <see cref="MockHttpHandler"/>.</summary>
public sealed record MockResponse(
    string Body,
    string? LinkHeader = null,
    HttpStatusCode StatusCode = HttpStatusCode.OK,
    bool AttachRequest = false,
    Task? Gate = null,
    string? RetryAfter = null);
