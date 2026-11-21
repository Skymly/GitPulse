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

    public Task LoadFirstPageAsync(PagedGitHubSession session, Func<PagedGitHubSession, Task> fetch)
    {
        DisposePaged();
        session.Reset();
        var paged = new PageClient(session);
        _paged = paged;
        return RunPageAsync(paged, fetch);
    }

    public Task LoadMoreAsync(Func<PagedGitHubSession, Task> fetch)
    {
        var paged = _paged;
        if (paged is null || !paged.Session.Advance())
            return Task.CompletedTask;

        return RunPageAsync(paged, fetch);
    }

    private async Task RunPageAsync(PageClient paged, Func<PagedGitHubSession, Task> fetch)
    {
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
