namespace LeaseLense.Web.Models.Reviews;

public sealed class ReviewListItemViewModel
{
    public Guid PropertyId { get; init; }
    public string PropertyTitle { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public decimal? MonthlyRent { get; init; }
    public decimal AverageRating { get; init; }
    public string? VerificationBadge { get; init; }
    public string AnonymizedReviewer { get; init; } = string.Empty;
    public string ReviewText { get; init; } = string.Empty;
}
