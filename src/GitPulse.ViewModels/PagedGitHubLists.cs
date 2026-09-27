using GitPulse.Core.Http;
using Observables.RestAPI;

namespace GitPulse.ViewModels;

/// <summary>
/// Drains <c>Link: rel="next"</c> on a paged GitHub list, capped like
/// notification polling so a runaway cursor cannot hang the page.
/// </summary>
internal static class PagedGitHubLists
{
    internal const int MaxPages = 10;

    public static async Task<T[]> LoadAllAsync<T>(
        PagedGitHubSession session,
        Func<CancellationToken, Task<ApiResponse<T[]>>> fetch,
        CancellationToken cancellationToken)
    {
        session.Reset();
        var items = new List<T>();
        for (var page = 0; page < MaxPages; page++)
        {
            if (page > 0 && (!session.HasNextPage || !session.Advance()))
                break;

            session.PrepareRequest();
            using var pageCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pageCts.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await fetch(pageCts.Token).ConfigureAwait(false);
            ApiResponses.EnsureSuccess(response);
            session.ApplyLink(response.Headers);
            if (response.Content is { Length: > 0 } pageItems)
                items.AddRange(pageItems);
        }

        return [.. items];
    }
}
