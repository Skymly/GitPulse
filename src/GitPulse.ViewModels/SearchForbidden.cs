using System.Globalization;
using System.Net.Http.Headers;

namespace GitPulse.ViewModels;

/// <summary>
/// Search 403 is a rate limit only when GitHub says remaining is zero.
/// SSO / permission 403s stay on the query with a different Page Error.
/// </summary>
internal static class SearchForbidden
{
    public const string RateLimitMessage =
        "GitHub Search rate limit exceeded. Wait before trying again.";

    public const string PermissionMessage =
        "Not allowed to perform this search.";

    public static string Message(HttpResponseHeaders? headers) =>
        IsRateLimited(headers) ? RateLimitMessage : PermissionMessage;

    public static bool IsRateLimited(HttpResponseHeaders? headers)
    {
        if (headers is null
            || !headers.TryGetValues("X-RateLimit-Remaining", out var values))
        {
            return false;
        }

        var raw = values.FirstOrDefault();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remaining)
            && remaining <= 0;
    }
}
