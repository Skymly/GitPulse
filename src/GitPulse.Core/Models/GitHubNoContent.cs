namespace GitPulse.Core.Models;

/// <summary>
/// Marker for a GitHub success response with no JSON body.
/// Observables 0.3.0 treats <c>ApiResponse&lt;Unit&gt;</c> as a void result and returns null,
/// so empty-body methods cannot use <c>Unit</c> if callers need the status code.
/// </summary>
public sealed class GitHubNoContent;
