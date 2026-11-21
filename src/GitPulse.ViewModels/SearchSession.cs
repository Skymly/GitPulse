using GitPulse.Core.Http;

namespace GitPulse.ViewModels;

/// <summary>
/// Result state and paging client for one search type or inbox.
/// A retired client stays alive until its last page request finishes.
/// </summary>
internal sealed class SearchSession
{
    private PageClient? _paged;

    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public bool HasSearched { get; set; }
    public bool HasSession => _paged is not null;
    public bool HasNextPage => _paged?.Session.HasNextPage == true;

    public void StartPaging(PagedGitHubSession session)
    {
        DisposePaged();
        session.Reset();
        _paged = new PageClient(session);
    }

    public async Task RunPageAsync(Func<PagedGitHubSession, Task> fetch, bool loadMore)
    {
        var paged = _paged;
        if (paged is null || (loadMore && !paged.Session.Advance()))
            return;

        paged.ActiveRequests++;
        try
        {
            await fetch(paged.Session);
        }
        finally
        {
            paged.ActiveRequests--;
            if (paged.ActiveRequests == 0 && !ReferenceEquals(paged, _paged))
                paged.Session.Dispose();
        }
    }

    public void DisposePaged()
    {
        var paged = _paged;
        _paged = null;
        if (paged?.ActiveRequests == 0)
            paged.Session.Dispose();
    }

    private sealed class PageClient(PagedGitHubSession session)
    {
        public PagedGitHubSession Session { get; } = session;
        public int ActiveRequests { get; set; }
    }
}
