namespace LeaseLense.Domain.Entities;

public sealed class Property
{
    public Guid PropertyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? StateOrRegion { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PropertyType { get; set; }
    public string? LandlordName { get; set; }
    public string? ManagementCompanyName { get; set; }
    public Guid CreatedByRenterId { get; set; }
    public DateTime CreatedAt { get; set; }
}
