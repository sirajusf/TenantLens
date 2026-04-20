namespace LeaseLense.Application.Reviews;

public sealed class CreateReviewDto
{
    public Guid? PropertyId { get; init; }
    public string? NewPropertyTitle { get; init; }
    public string? NewPropertyStreetAddress { get; init; }
    public string? NewPropertyCity { get; init; }
    public string? NewPropertyCountry { get; init; }
    public decimal? MonthlyRent { get; init; }
    public string? UnitType { get; init; }
    public string ReviewText { get; init; } = string.Empty;
    public string VerificationStatus { get; init; } = "verified";
    public int OverallRating { get; init; }
    public string ReporterEmail { get; init; } = string.Empty;
}
