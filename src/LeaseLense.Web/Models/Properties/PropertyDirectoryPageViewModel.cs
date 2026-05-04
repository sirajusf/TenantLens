namespace LeaseLense.Web.Models.Properties;

public sealed class PropertyDirectoryPageViewModel
{
    public string? QueryText { get; init; }
    public string? City { get; init; }
    public string? PropertyType { get; init; }
    public string? LandlordName { get; init; }
    public decimal? MinRating { get; init; }
    public bool HasVerifiedReviews { get; init; }
    public string? SortBy { get; init; }
    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];
    public bool LlmUnavailable { get; init; }
    public bool NlFallback { get; init; }
    public IReadOnlyList<PropertyDirectoryItemViewModel> Properties { get; init; } = [];
}
