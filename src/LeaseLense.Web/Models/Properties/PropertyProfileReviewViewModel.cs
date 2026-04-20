namespace LeaseLense.Web.Models.Properties;

public sealed class PropertyProfileReviewViewModel
{
    public string ReviewerAlias { get; init; } = string.Empty;
    public decimal? MonthlyRent { get; init; }
    public decimal AverageRating { get; init; }
    public bool IsVerified { get; init; }
    public string ReviewText { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
