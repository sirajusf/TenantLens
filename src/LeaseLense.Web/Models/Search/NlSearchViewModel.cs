using LeaseLense.Application.Search;

namespace LeaseLense.Web.Models.Search;

public sealed class NlSearchViewModel
{
    /// <summary>The original user query text.</summary>
    public string? Query { get; init; }

    /// <summary>True when the user submitted a query and results were obtained.</summary>
    public bool HasSearched { get; init; }

    /// <summary>Entity types the NLP layer selected for the current query.</summary>
    public IReadOnlyList<SearchEntityType> TargetEntityTypes { get; init; } = [];

    /// <summary>Human-readable descriptions of each filter extracted by the NLP layer.</summary>
    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];

    /// <summary>True when the LLM was unavailable and results are plain-text keyword fallback only.</summary>
    public bool LlmUnavailable { get; init; }

    /// <summary>
    /// True when the keyword search returned 0 results and the NL parser was invoked as a
    /// second-chance fallback — the displayed results come from AI-interpreted filters.
    /// </summary>
    public bool NlFallback { get; init; }

    // ── Typed result lists (one, two, or all can be populated) ───────────────

    public IReadOnlyList<PropertyMatch> PropertyResults { get; init; } = [];
    public IReadOnlyList<ReviewMatch> ReviewResults { get; init; } = [];
    public IReadOnlyList<ScamReportMatch> ScamReportResults { get; init; } = [];

    public int TotalResults =>
        PropertyResults.Count + ReviewResults.Count + ScamReportResults.Count;
}
