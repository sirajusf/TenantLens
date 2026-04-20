namespace LeaseLense.Web.Models.Properties;

public sealed class PropertyDirectoryItemViewModel
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public decimal? AverageRating { get; init; }
    public decimal? AverageScamSeverity { get; init; }
}
