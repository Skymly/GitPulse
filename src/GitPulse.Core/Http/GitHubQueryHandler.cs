using System.Globalization;

namespace GitPulse.Core.Http;

/// <summary>
/// DelegatingHandler that injects GitHub pagination/filter query parameters
/// (<c>page</c>, <c>per_page</c>, <c>state</c>) into outgoing requests.
/// </summary>
/// <remarks>Scoped pagination uses request options (ADR-018). Unscoped callers retain the legacy handler defaults during migration.</remarks>
public sealed class GitHubQueryHandler : DelegatingHandler
{
    internal static readonly HttpRequestOptionsKey<int> PageOption = new("GitPulse.Pagination.Page");
    internal static readonly HttpRequestOptionsKey<int> PerPageOption = new("GitPulse.Pagination.PerPage");
    internal static readonly HttpRequestOptionsKey<string> StateOption = new("GitPulse.Pagination.State");
    internal const int DefaultPerPage = 30;

    // Older list callers still prepare the handler before invoking their proxy.
    public int Page { get; set; } = 1;
    public int PerPage { get; set; } = DefaultPerPage;
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
        var hasPage = request.Options.TryGetValue(PageOption, out var page);
        var hasPerPage = request.Options.TryGetValue(PerPageOption, out var perPage);
        request.Options.TryGetValue(StateOption, out var state);
        if (!hasPage && !hasPerPage && state is null)
        {
            page = Page;
            perPage = PerPage;
            state = State;
            hasPage = hasPerPage = true;
        }

        if (uri is not null)
        {
            var builder = new UriBuilder(uri);
            var existing = ParseQuery(builder.Query);
            if (hasPage && (page > 1 || existing.ContainsKey("page")))
                existing["page"] = page.ToString(CultureInfo.InvariantCulture);
            if (hasPerPage && (perPage != DefaultPerPage || existing.ContainsKey("per_page")))
                existing["per_page"] = perPage.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(state))
                existing["state"] = state;

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
