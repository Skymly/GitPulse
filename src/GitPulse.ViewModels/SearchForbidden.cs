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
        Message(Remaining(headers));

    public static string Message(string? remaining) =>
        IsRateLimited(remaining) ? RateLimitMessage : PermissionMessage;

    public static string? Remaining(HttpResponseHeaders? headers)
    {
        if (headers is null
            || !headers.TryGetValues("X-RateLimit-Remaining", out var values))
        {
            return null;
        }

        return values.FirstOrDefault();
    }

    public static bool IsRateLimited(HttpResponseHeaders? headers) =>
        IsRateLimited(Remaining(headers));

    public static bool IsRateLimited(string? remaining) =>
        int.TryParse(remaining, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        && value <= 0;
}
