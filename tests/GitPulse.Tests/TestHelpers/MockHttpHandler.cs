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
    private readonly Dictionary<string, Func<HttpRequestMessage, string>> _routes = new();

    /// <summary>
    /// Register a canned JSON response for requests whose absolute path
    /// ends with <paramref name="pathSuffix"/>.
    /// </summary>
    public MockHttpHandler When(string pathSuffix, Func<HttpRequestMessage, string> respond)
    {
        _routes[pathSuffix] = respond;
        return this;
    }

    /// <summary>Shorthand for a constant JSON body.</summary>
    public MockHttpHandler When(string pathSuffix, string jsonBody)
        => When(pathSuffix, _ => jsonBody);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        foreach (var (suffix, responder) in _routes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var body = responder(request);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No mock for {path}", Encoding.UTF8, "text/plain"),
        });
    }
}
