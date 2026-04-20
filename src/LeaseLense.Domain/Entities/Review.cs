namespace LeaseLense.Domain.Entities;

public sealed class Review
{
    public Guid ReviewId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid RenterId { get; set; }
    public DateOnly? LeaseStartDate { get; set; }
    public DateOnly? LeaseEndDate { get; set; }
    public decimal? MonthlyRent { get; set; }
    public int? MoveInYear { get; set; }
    public string? UnitType { get; set; }
    public string? ReviewText { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
