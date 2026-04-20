namespace LeaseLense.Domain.Entities;

public sealed class Renter
{
    public Guid RenterId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
