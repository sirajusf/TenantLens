namespace LeaseLense.Domain.Entities;

public sealed class LeaseAnalysis
{
    public Guid LeaseAnalysisId { get; set; }
    public Guid LeaseDocumentId { get; set; }
    public Guid RenterId { get; set; }
    public Guid PropertyId { get; set; }
    public decimal? SummaryRiskScore { get; set; }
    public string? RiskLevel { get; set; }
    public string? SummaryText { get; set; }
    public string? ModelVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}
