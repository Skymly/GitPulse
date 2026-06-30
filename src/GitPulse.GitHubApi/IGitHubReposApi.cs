using GitPulse.Core.Models;
using Observables.RestAPI;
using R3;

namespace GitPulse.GitHubApi;

/// <summary>
/// Declarative GitHub Repos REST API.
/// The Observables.RestAPI.R3 source generator produces an HttpClient proxy
/// implementation at compile time; consume via <c>RestService.For&lt;IGitHubReposApi&gt;(httpClient)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observables 0.1.4 limitation:</b> <c>ValidatePathTemplate</c> requires the set of path
/// placeholders to equal the set of all non-CancellationToken parameter names. It does not
/// exclude <c>[Query]</c>/<c>[Body]</c> parameters, so query/body parameters cannot coexist
/// with path parameters on the same method in 0.1.4. Pagination and filtering will be handled
/// via a custom <see cref="HttpMessageHandler"/> or dedicated query-only methods until the
/// upstream validation is relaxed.
/// </para>
/// </remarks>
public interface IGitHubReposApi
{
    [Get("/user/repos")]
    Observable<Repo[]> ListMyRepos();

    [Get("/repos/{owner}/{repo}")]
    Observable<Repo> GetRepo(string owner, string repo);

    [Get("/repos/{owner}/{repo}/issues")]
    Observable<Issue[]> ListIssues(string owner, string repo);
}
