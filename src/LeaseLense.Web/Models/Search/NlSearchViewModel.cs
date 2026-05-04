using LeaseLense.Application.Search;

namespace LeaseLense.Web.Models.Search;

public sealed class NlSearchViewModel
{
    /// <summary>The original user query text.</summary>
    public string? Query { get; init; }

    /// <summary>True when the user submitted a query and results were obtained.</summary>
    public bool HasSearched { get; init; }

    /// <summary>Entity type the NLP layer determined this query targets.</summary>
    public SearchEntityType EntityType { get; init; } = SearchEntityType.Property;

    /// <summary>Human-readable descriptions of each filter extracted by the NLP layer.</summary>
    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];

    /// <summary>True when the LLM was unavailable and results are plain-text fallback only.</summary>
    public bool LlmUnavailable { get; init; }

    // ── Typed result lists (only one is populated per search) ────────────────

    public IReadOnlyList<PropertyMatch> PropertyResults { get; init; } = [];
    public IReadOnlyList<ReviewMatch> ReviewResults { get; init; } = [];
    public IReadOnlyList<ScamReportMatch> ScamReportResults { get; init; } = [];

    public int TotalResults =>
        PropertyResults.Count + ReviewResults.Count + ScamReportResults.Count;
}
