using System.Net;
using GitPulse.Core.Http;
using Observables.RestAPI;

namespace GitPulse.ViewModels;

internal static class ApiResponses
{
    public static void EnsureSuccess<T>(ApiResponse<T> response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var code = (int)(response.StatusCode ?? 0);
        throw new HttpRequestException(
            $"Response status code does not indicate success: {code}.",
            inner: null,
            statusCode: response.StatusCode);
    }

    public static PagedListPage<T> PageOrThrow<T>(ApiResponse<T[]> response)
    {
        using (response)
        {
            EnsureSuccess(response);
            return new PagedListPage<T>(
                response.Content ?? [],
                LinkHeaderParser.CopyLinkHeader(response.Headers));
        }
    }
}
