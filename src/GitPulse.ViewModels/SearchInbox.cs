using System.Collections.ObjectModel;
using System.Net;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

internal enum SearchInboxKind
{
    Issues,
    PullRequests,
}

/// <summary>
/// One canned-query Search Inbox hub. Search chrome stays on <see cref="SearchViewModel"/>.
/// </summary>
internal sealed class SearchInbox : IDisposable
{
    private readonly string _query;
    private readonly SearchInboxKind _kind;
    private readonly SearchSession _session = new();

    public SearchInbox(
        string query,
        SearchInboxKind kind,
        ObservableCollection<SearchIssueItem> items)
    {
        _query = query;
        _kind = kind;
        Items = items;
    }

    public ObservableCollection<SearchIssueItem> Items { get; }

    public bool HasSession => _session.Paged is not null;

    public bool HasSearched => _session.HasSearched;

    public int TotalCount => _session.TotalCount;

    public bool HasNextPage => _session.HasNextPage;

    public bool CanLoadMore => HasSession && HasNextPage;

    public async Task<SearchInboxResult> LoadAsync(
        IGitHubClientFactory factory,
        int version,
        Func<int, bool> isCurrent,
        CancellationToken cancellationToken)
    {
        _session.DisposePaged();
        Items.Clear();

        try
        {
            var paged = await StartPagedAsync(factory, version, isCurrent, cancellationToken);
            if (paged is null)
            {
                return isCurrent(version)
                    ? SearchInboxResult.Fail("No token configured. Open Settings to add a GitHub PAT.")
                    : SearchInboxResult.Stale;
            }

            var api = RestService.For<IGitHubSearchApi>(paged.Client);
            await FetchPageAsync(api, replace: true, version, isCurrent, cancellationToken);
            if (!isCurrent(version))
                return SearchInboxResult.Stale;

            _session.HasSearched = true;
            return SearchInboxResult.Ok;
        }
        catch (OperationCanceledException)
        {
            return ResultIfCurrent(version, isCurrent, "Request timed out.");
        }
        catch (SearchInboxRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub Search rate limit exceeded. Wait before trying again.");
        }
        catch (SearchInboxRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub rejected the search query. Check its syntax and qualifiers.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub Search rate limit exceeded. Wait before trying again.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub rejected the search query. Check its syntax and qualifiers.");
        }
        catch (Exception ex)
        {
            return ResultIfCurrent(version, isCurrent, $"Load failed: {ex.Message}");
        }
    }

    public async Task<SearchInboxResult> LoadMoreAsync(
        int version,
        Func<int, bool> isCurrent,
        CancellationToken cancellationToken)
    {
        if (_session.Paged is null || !_session.HasNextPage)
            return SearchInboxResult.Ok;

        try
        {
            if (!_session.Paged.Advance())
                return SearchInboxResult.Ok;

            _session.Paged.PrepareRequest();
            var api = RestService.For<IGitHubSearchApi>(_session.Paged.Client);
            await FetchPageAsync(api, replace: false, version, isCurrent, cancellationToken);
            return isCurrent(version) ? SearchInboxResult.Ok : SearchInboxResult.Stale;
        }
        catch (OperationCanceledException)
        {
            return ResultIfCurrent(version, isCurrent, "Request timed out.");
        }
        catch (SearchInboxRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub Search rate limit exceeded. Wait before trying again.");
        }
        catch (SearchInboxRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub rejected the search query. Check its syntax and qualifiers.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub Search rate limit exceeded. Wait before trying again.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return ResultIfCurrent(
                version,
                isCurrent,
                "GitHub rejected the search query. Check its syntax and qualifiers.");
        }
        catch (Exception ex)
        {
            return ResultIfCurrent(version, isCurrent, $"Load more failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _session.DisposePaged();
    }

    private async Task FetchPageAsync(
        IGitHubSearchApi api,
        bool replace,
        int version,
        Func<int, bool> isCurrent,
        CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(_query);
        var response = _kind == SearchInboxKind.PullRequests
            ? await api.SearchPullRequests(encoded).FirstAsync(cancellationToken)
            : await api.SearchIssues(encoded).FirstAsync(cancellationToken);
        if (!isCurrent(version))
            return;

        if (!response.IsSuccessStatusCode)
        {
            throw new SearchInboxRequestException(
                response.StatusCode ?? HttpStatusCode.ServiceUnavailable);
        }

        if (replace)
            Items.Clear();

        foreach (var item in response.Content?.Items ?? [])
            Items.Add(item);

        _session.TotalCount = response.Content?.TotalCount ?? 0;
        _session.Paged?.ApplyLink(response.Headers);
    }

    private async Task<PagedGitHubSession?> StartPagedAsync(
        IGitHubClientFactory factory,
        int version,
        Func<int, bool> isCurrent,
        CancellationToken cancellationToken)
    {
        var paged = await factory.CreatePagedSessionAsync(cancellationToken);
        if (!isCurrent(version))
        {
            paged.Dispose();
            return null;
        }

        if (paged.Client.DefaultRequestHeaders.Authorization is null)
        {
            paged.Dispose();
            return null;
        }

        _session.Paged = paged;
        _session.Query = _query;
        paged.Reset();
        paged.PrepareRequest();
        return paged;
    }

    private static SearchInboxResult ResultIfCurrent(
        int version,
        Func<int, bool> isCurrent,
        string error)
    {
        return isCurrent(version) ? SearchInboxResult.Fail(error) : SearchInboxResult.Stale;
    }

    private sealed class SearchSession
    {
        public PagedGitHubSession? Paged { get; set; }
        public string Query { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public bool HasSearched { get; set; }
        public bool HasNextPage => Paged?.HasNextPage == true;

        public void DisposePaged()
        {
            Paged?.Dispose();
            Paged = null;
        }
    }

    private sealed class SearchInboxRequestException(HttpStatusCode statusCode) : Exception
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}

internal readonly record struct SearchInboxResult(string? Error, bool Completed)
{
    public static SearchInboxResult Stale { get; } = new(null, false);

    public static SearchInboxResult Ok { get; } = new(null, true);

    public static SearchInboxResult Fail(string error) => new(error, true);
}
