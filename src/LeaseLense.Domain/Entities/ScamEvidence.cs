namespace LeaseLense.Domain.Entities;

public sealed class ScamEvidence
{
    public Guid ScamEvidenceId { get; set; }
    public Guid ScamReportId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public DateTime UploadedAt { get; set; }
}
