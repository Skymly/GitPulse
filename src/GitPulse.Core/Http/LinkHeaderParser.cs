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
    public static string? GetNextUrl(HttpResponseHeaders? headers)
    {
        if (headers is null || !headers.TryGetValues("Link", out var values))
            return null;

        var linkHeader = values.FirstOrDefault();
        if (string.IsNullOrEmpty(linkHeader))
            return null;

        var match = NextRelRegex().Match(linkHeader);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts the <c>page</c> query parameter value from a <c>rel="next"</c>
    /// URL, or <c>null</c> if not present. Used to determine the next page
    /// number without re-parsing the full URL.
    /// </summary>
    public static int? GetNextPageNumber(HttpResponseHeaders? headers)
    {
        var nextUrl = GetNextUrl(headers);
        if (nextUrl is null)
            return null;

        var match = PageParamRegex().Match(nextUrl);
        return match.Success && int.TryParse(match.Groups[1].Value, out var page) ? page : null;
    }

    [GeneratedRegex(@"<([^>]+)>;\s*rel=""next""", RegexOptions.IgnoreCase)]
    private static partial Regex NextRelRegex();

    [GeneratedRegex(@"[?&]page=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PageParamRegex();
}
