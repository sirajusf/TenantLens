namespace LeaseLense.Domain.Entities;

public sealed class ScamReport
{
    public Guid ScamReportId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid RenterId { get; set; }
    public string ScamType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? SeverityScore { get; set; }
    public DateTime DateReported { get; set; }
    public DateTime CreatedAt { get; set; }
}
