using GitPulse.Core.Models;
using Observables.RestAPI;
using R3;

namespace GitPulse.GitHubApi;

/// <summary>
/// Declarative GitHub Search REST API.
/// Search expressions are explicit query parameters, while pagination is
/// injected by <c>GitHubQueryHandler</c>.
/// </summary>
public interface IGitHubSearchApi
{
    [Get("/search/repositories")]
    Observable<ApiResponse<SearchResult<Repo>>> SearchRepositories([Query] string q);

    [Get("/search/issues")]
    Observable<ApiResponse<SearchResult<SearchIssueItem>>> SearchIssues([Query] string q);

    [Get("/search/issues")]
    Observable<ApiResponse<SearchResult<SearchIssueItem>>> SearchPullRequests([Query] string q);

    [Get("/search/code")]
    Observable<ApiResponse<SearchResult<CodeSearchItem>>> SearchCode([Query] string q);
}
