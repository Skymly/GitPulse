using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace GitPulse.Core.Http;

/// <summary>
/// Parses RFC 8288 <c>Link</c> headers to extract pagination URLs.
/// Used to detect <c>rel="next"</c> for "load more" support.
/// </summary>
public static partial class LinkHeaderParser
{
    /// <summary>
    /// Extracts the <c>rel="next"</c> URL from a <c>Link</c> header, or
    /// <c>null</c> if there is no next page.
    /// </summary>
    public static string? GetNextUrl(HttpResponseHeaders? headers) =>
        GetNextUrlFromCopy(CopyLinkHeader(headers));

    /// <summary>
    /// Copies <c>Link</c> header values so callers can dispose
    /// <c>ApiResponse&lt;T&gt;</c> before pagination uses them.
    /// </summary>
    public static string? CopyLinkHeader(HttpResponseHeaders? headers)
    {
        if (headers is null || !headers.TryGetValues("Link", out var values))
            return null;

        return string.Join(", ", values);
    }

    /// <summary>
    /// Extracts the <c>rel="next"</c> URL from an already-copied <c>Link</c>
    /// header value, or <c>null</c> if there is no next page.
    /// </summary>
    public static string? GetNextUrlFromCopy(string? linkHeader)
    {
        if (string.IsNullOrEmpty(linkHeader))
            return null;

        var match = NextRelRegex().Match(linkHeader);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(
        @"<([^>]+)>(?:\s*;\s*[^,<>]+)*?\s*;\s*rel\s*=\s*(?:""next""|'next'|next)(?=\s|;|,|$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex NextRelRegex();
}
