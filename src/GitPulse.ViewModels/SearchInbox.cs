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

    public bool HasSession => _session.HasSession;

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
            var paged = await factory.CreatePagedSessionAsync(cancellationToken);
            if (!isCurrent(version))
            {
                paged.Dispose();
                return SearchInboxResult.Stale;
            }

            if (paged.Client.DefaultRequestHeaders.Authorization is null)
            {
                paged.Dispose();
                return SearchInboxResult.Fail("No token configured. Open Settings to add a GitHub PAT.");
            }

            await _session.LoadFirstPageAsync(paged, page => FetchPageAsync(
                page, replace: true, version, isCurrent, cancellationToken));
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
                SearchForbidden.Message(ex.Remaining));
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
                SearchForbidden.PermissionMessage);
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
        if (!HasSession || !_session.HasNextPage)
            return SearchInboxResult.Ok;

        try
        {
            await _session.LoadMoreAsync(paged => FetchPageAsync(
                paged, replace: false, version, isCurrent, cancellationToken));
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
                SearchForbidden.Message(ex.Remaining));
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
                SearchForbidden.PermissionMessage);
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

    public void DropSession()
    {
        _session.DisposePaged();
        _session.HasSearched = false;
        _session.TotalCount = 0;
        Items.Clear();
    }

    private async Task FetchPageAsync(
        PagedGitHubSession paged,
        bool replace,
        int version,
        Func<int, bool> isCurrent,
        CancellationToken cancellationToken)
    {
        var api = RestService.For<IGitHubSearchApi>(paged.Client);
        using var response = await paged
            .PrepareRequest(() => _kind == SearchInboxKind.PullRequests
                ? api.SearchPullRequests(_query, cancellationToken)
                : api.SearchIssues(_query, cancellationToken))
            .FirstAsync();
        if (!isCurrent(version))
            return;

        cancellationToken.ThrowIfCancellationRequested();

        if (!response.IsSuccessStatusCode)
        {
            throw new SearchInboxRequestException(
                response.StatusCode ?? HttpStatusCode.ServiceUnavailable,
                SearchForbidden.Remaining(response.Headers));
        }

        if (replace)
            Items.Clear();

        foreach (var item in response.Content?.Items ?? [])
            Items.Add(item);

        _session.TotalCount = response.Content?.TotalCount ?? 0;
        paged.ApplyLink(response.Headers);
    }

    private static SearchInboxResult ResultIfCurrent(
        int version,
        Func<int, bool> isCurrent,
        string error)
    {
        return isCurrent(version) ? SearchInboxResult.Fail(error) : SearchInboxResult.Stale;
    }

    private sealed class SearchInboxRequestException(
        HttpStatusCode statusCode,
        string? remaining) : Exception
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
        public string? Remaining { get; } = remaining;
    }
}

internal readonly record struct SearchInboxResult(string? Error, bool Completed)
{
    public static SearchInboxResult Stale { get; } = new(null, false);

    public static SearchInboxResult Ok { get; } = new(null, true);

    public static SearchInboxResult Fail(string error) => new(error, true);
}
