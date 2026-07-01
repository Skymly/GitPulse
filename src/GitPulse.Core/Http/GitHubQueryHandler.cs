namespace GitPulse.Core.Http;

/// <summary>
/// DelegatingHandler that injects GitHub pagination/filter query parameters
/// (<c>page</c>, <c>per_page</c>, <c>state</c>) into outgoing requests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> Observables.RestAPI 0.1.4 <c>ValidatePathTemplate</c>
/// (OBS3004) rejects methods that combine path placeholders with <c>[Query]</c>
/// parameters. This handler injects query parameters at the HTTP layer instead,
/// keeping the declarative interface clean (path-only parameters).
/// See <see href="https://github.com/Skymly/Observables/issues/111"/>.
/// </para>
/// <para>
/// The handler is <b>per-request stateful</b>: set <see cref="Page"/>,
/// <see cref="PerPage"/>, and <see cref="State"/> before each request to
/// control pagination/filtering. A fresh handler instance is created per
/// ViewModel load cycle (the factory creates one per <c>CreateClientAsync</c>
/// call).
/// </para>
/// </remarks>
public sealed class GitHubQueryHandler : DelegatingHandler
{
    /// <summary>1-based page number (default 1).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Items per page (default 30, max 100 per GitHub API).</summary>
    public int PerPage { get; set; } = 30;

    /// <summary>State filter for issues/PRs: "open", "closed", "all" (default "open").</summary>
    public string? State { get; set; }

    public GitHubQueryHandler() : base(new HttpClientHandler())
    {
    }

    public GitHubQueryHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (uri is not null)
        {
            var builder = new UriBuilder(uri);

            // Parse existing query into a dictionary to avoid duplicates.
            var existing = ParseQuery(builder.Query);
            if (Page > 1)
                existing["page"] = Page.ToString();
            if (PerPage != 30)
                existing["per_page"] = PerPage.ToString();
            if (!string.IsNullOrEmpty(State) && State != "all")
                existing["state"] = State;

            builder.Query = BuildQuery(existing);
            request.RequestUri = builder.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrEmpty(trimmed))
            return result;

        foreach (var pair in trimmed.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0)
                result[pair[..eq]] = pair[(eq + 1)..];
        }

        return result;
    }

    private static string BuildQuery(Dictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
            return string.Empty;

        var pairs = parameters.Select(p => $"{p.Key}={p.Value}");
        return string.Join('&', pairs);
    }
}
