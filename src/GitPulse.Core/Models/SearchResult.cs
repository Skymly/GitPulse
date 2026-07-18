using System.Text.Json.Serialization;

namespace GitPulse.Core.Models;

public sealed class SearchResult<T>
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("incomplete_results")]
    public bool IncompleteResults { get; init; }

    [JsonPropertyName("items")]
    public T[] Items { get; init; } = [];
}
