namespace LeaseLense.Domain.Entities;

public sealed class Renter
{
    public Guid RenterId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? StateOrRegion { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
