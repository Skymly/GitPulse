using System.Globalization;

namespace GitPulse.Core.Http;

/// <summary>
/// DelegatingHandler that injects GitHub pagination/filter query parameters
/// (<c>page</c>, <c>per_page</c>, <c>state</c>) into outgoing requests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> paged list methods still inject <c>page</c> /
/// <c>per_page</c> / <c>state</c> here so those signatures stay path-only
/// (ADR-006). Observables.RestAPI 0.1.4 <c>ValidatePathTemplate</c> (OBS3004)
/// used to reject path placeholders combined with <c>[Query]</c>; 0.1.5 lifted
/// that for business query parameters. Search <c>q</c>, check-run <c>filter</c>,
/// contents <c>ref</c>, and <c>ListMyReposSortedPaged</c> <c>sort</c> use
/// <c>[Query]</c> on the declarative interfaces. This handler is not a
/// workaround for that generator limit anymore.
/// See <see href="https://github.com/Skymly/Observables/issues/111"/>.
/// </para>
/// <para>
/// The handler is <b>per-session stateful</b>: set <see cref="Page"/>,
/// <see cref="PerPage"/>, and <see cref="State"/> before each request to
/// control pagination/filtering. A fresh query-handler instance is created
/// per paged session. The inner <see cref="HttpMessageHandler"/> may be
/// owned and shared by <c>IGitHubClientFactory</c> for the process lifetime.
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
            if (Page > 1 || existing.ContainsKey("page"))
                existing["page"] = Page.ToString(CultureInfo.InvariantCulture);
            if (PerPage != 30 || existing.ContainsKey("per_page"))
                existing["per_page"] = PerPage.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(State))
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
            if (pair.Length == 0)
                continue;

            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            if (eq == 0)
                continue;

            var key = Uri.UnescapeDataString(pair[..eq]);
            if (key.Length == 0)
                continue;

            result[key] = Uri.UnescapeDataString(pair[(eq + 1)..]);
        }

        return result;
    }

    private static string BuildQuery(Dictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
            return string.Empty;

        var pairs = parameters.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
        return string.Join('&', pairs);
    }
}
