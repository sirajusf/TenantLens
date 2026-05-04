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
    /// <summary>Structured query ready to be dispatched to <see cref="LeaseLense.Application.Abstractions.ICoreSearchService"/>.</summary>
    public required SearchQueryDto Query { get; init; }

    /// <summary>Human-readable descriptions of each filter extracted — shown as chips in the UI.</summary>
    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];
}
