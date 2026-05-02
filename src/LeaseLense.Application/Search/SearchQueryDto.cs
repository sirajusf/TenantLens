namespace LeaseLense.Application.Search;

/// <summary>
/// Unified query contract for all search paths.  All fields are optional; each search method
/// reads only the fields relevant to its entity type.  A future NLP layer converts natural
/// language into a populated instance of this DTO — the rest of the pipeline is unchanged.
/// </summary>
public sealed class SearchQueryDto
{
    // ── Core ──────────────────────────────────────────────────────────────────
    public string? QueryText { get; init; }
    public SearchEntityType EntityType { get; init; }
    public int Limit { get; init; } = 100;

    // ── Location ──────────────────────────────────────────────────────────────
    public string? City { get; init; }

    // ── Property filters ──────────────────────────────────────────────────────
    public string? PropertyType { get; init; }
    public string? LandlordName { get; init; }

    // ── Review filters ────────────────────────────────────────────────────────
    public decimal? MinRent { get; init; }
    public decimal? MaxRent { get; init; }
    public decimal? MinRating { get; init; }
    public decimal? MinCommunicationRating { get; init; }
    public decimal? MinMaintenanceRating { get; init; }
    public bool VerifiedOnly { get; init; }
    public bool HasVerifiedReviews { get; init; }

    /// <summary>Filter reviews that have at least one of these issue tags (e.g. "mold", "pests").</summary>
    public IReadOnlyList<string>? IssueTypes { get; init; }

    // ── Scam-report filters ───────────────────────────────────────────────────
    public decimal? MinSeverity { get; init; }
    public decimal? MaxSeverity { get; init; }
    public string? ScamType { get; init; }
    public DateOnly? DateReportedAfter { get; init; }

    // ── Sorting ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Entity-specific sort key.
    /// Reviews: "newest" | "rating" | "rent".
    /// Properties: "name" | "rating".
    /// Scam reports: "newest" | "severity".
    /// </summary>
    public string? SortBy { get; init; }
}
