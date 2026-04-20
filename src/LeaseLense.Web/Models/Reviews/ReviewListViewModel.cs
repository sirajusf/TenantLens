namespace LeaseLense.Web.Models.Reviews;

public sealed class ReviewListViewModel
{
    public string? PropertyQuery { get; init; }
    public string? City { get; init; }
    public decimal? MinRent { get; init; }
    public decimal? MaxRent { get; init; }
    public decimal? MinRating { get; init; }
    public string SortBy { get; init; } = "newest";
    public IReadOnlyList<ReviewListItemViewModel> Reviews { get; init; } = [];
}
