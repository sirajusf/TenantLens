namespace LeaseLense.Application.Reputation;

public sealed class PropertyReputationDto
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string? LandlordName { get; init; }
    public decimal OverallScore { get; init; }
    public decimal MaintenanceScore { get; init; }
    public decimal CommunicationScore { get; init; }
    public decimal TrustScore { get; init; }
    public int ReviewCount { get; init; }
    public int ScamReportCount { get; init; }
}
