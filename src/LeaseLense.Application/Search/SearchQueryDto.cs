namespace LeaseLense.Application.Search;

/// <summary>
/// Optional unified query shape for dispatching; page-specific filters apply per entity type.
/// </summary>
public sealed class SearchQueryDto
{
    public string? QueryText { get; init; }
    public SearchEntityType EntityType { get; init; }
    public string? City { get; init; }
    public decimal? MinRent { get; init; }
    public decimal? MaxRent { get; init; }
    public decimal? MinRating { get; init; }
    public decimal? MinSeverity { get; init; }
    public int Limit { get; init; } = 100;
}
