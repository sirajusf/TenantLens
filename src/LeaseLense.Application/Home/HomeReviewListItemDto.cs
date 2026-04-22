namespace LeaseLense.Application.Home;

public sealed class HomeReviewListItemDto
{
    public Guid ReviewId { get; init; }
    public string PropertyTitle { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public decimal? AverageRating { get; init; }
    public decimal? MonthlyRent { get; init; }
    public string ReviewText { get; init; } = string.Empty;
    public string? VerificationBadge { get; init; }
}
