namespace LeaseLense.Application.Reviews;

public sealed class ReviewListSummaryDto
{
    public int TotalMatching { get; init; }
    public decimal AverageRating { get; init; }
    public int VerifiedStaysCount { get; init; }

    /// <summary>0–100 when TotalMatching &gt; 0; otherwise 0.</summary>
    public decimal VerifiedStaysPercent { get; init; }
}
