namespace LeaseLense.Web.Models.Reviews;

public sealed class ReviewListViewModel
{
    public string? PropertyQuery { get; init; }
    public string? City { get; init; }
    public decimal? MinRent { get; init; }
    public decimal? MaxRent { get; init; }
    public decimal? MinRating { get; init; }
    public decimal? MinCommunicationRating { get; init; }
    public decimal? MinMaintenanceRating { get; init; }
    public bool VerifiedOnly { get; init; }
    public List<string>? IssueTypes { get; init; }
    public string SortBy { get; init; } = "newest";
    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];
    public bool LlmUnavailable { get; init; }
    public bool NlFallback { get; init; }
    public ReviewSummaryViewModel? Summary { get; init; }
    public bool HasLoadError { get; init; }
    public string? LoadErrorMessage { get; init; }
    public IReadOnlyList<ReviewListItemViewModel> Reviews { get; init; } = [];
}
