using LeaseLense.Application.Search;

namespace LeaseLense.Web.Services;

/// <summary>
/// Converts a user's natural-language search string into a structured <see cref="SearchQueryDto"/>
/// using an LLM, along with human-readable chips describing what was extracted.
/// </summary>
public interface INlQueryParserLlmClient
{
    /// <summary>
    /// Returns null when the LLM is disabled or all attempts fail.
    /// Callers should fall back to a plain keyword search in that case.
    /// </summary>
    Task<NlQueryParseResult?> TryParseAsync(string userQuery, CancellationToken cancellationToken = default);
}

/// <summary>
/// Parsed result from the NL query parser LLM.
/// </summary>
public sealed class NlQueryParseResult
{
    /// <summary>
    /// Legacy single-entity query shape. Kept for backward compatibility.
    /// For new flows, prefer <see cref="QueriesByEntity"/> and <see cref="TargetEntityTypes"/>.
    /// </summary>
    public SearchQueryDto? Query { get; init; }

    /// <summary>
    /// One or more entities selected by the NL parser for global mixed search.
    /// Empty means callers should fall back to a default entity strategy.
    /// </summary>
    public IReadOnlyList<SearchEntityType> TargetEntityTypes { get; init; } = [];

    /// <summary>
    /// Structured query per selected entity type.
    /// </summary>
    public IReadOnlyDictionary<SearchEntityType, SearchQueryDto> QueriesByEntity { get; init; }
        = new Dictionary<SearchEntityType, SearchQueryDto>();

    /// <summary>Human-readable descriptions of each filter extracted — shown as chips in the UI.</summary>
    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];
}
