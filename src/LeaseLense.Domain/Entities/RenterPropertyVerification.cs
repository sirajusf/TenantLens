namespace LeaseLense.Domain.Entities;

public sealed class RenterPropertyVerification
{
    public Guid RenterPropertyVerificationId { get; set; }
    public Guid RenterId { get; set; }
    public Guid PropertyId { get; set; }
    public string Status { get; set; } = "pending_manual_review";
    public decimal ConfidenceScore { get; set; }
    public DateOnly? VerifiedFrom { get; set; }
    public DateOnly? VerifiedTo { get; set; }
    public string? ReviewReason { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
