namespace LeaseLense.Web.Models.Reviews;

public sealed class ReviewSummaryViewModel
{
    public int TotalMatching { get; init; }
    public decimal AverageRating { get; init; }
    public int VerifiedStaysCount { get; init; }
    public decimal VerifiedStaysPercent { get; init; }
}
