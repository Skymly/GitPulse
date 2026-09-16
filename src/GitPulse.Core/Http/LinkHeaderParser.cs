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

        foreach (var linkHeader in values)
        {
            if (string.IsNullOrEmpty(linkHeader))
                continue;

            var match = NextRelRegex().Match(linkHeader);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    [GeneratedRegex(
        @"<([^>]+)>(?:\s*;\s*[^,<>]+)*?\s*;\s*rel\s*=\s*(?:""next""|'next'|next)(?=\s|;|,|$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex NextRelRegex();
}
