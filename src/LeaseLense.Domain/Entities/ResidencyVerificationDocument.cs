namespace LeaseLense.Domain.Entities;

public sealed class ResidencyVerificationDocument
{
    public Guid ResidencyVerificationDocumentId { get; set; }
    public Guid RenterPropertyVerificationId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileHashSha256 { get; set; } = string.Empty;
    public string ExtractedName { get; set; } = string.Empty;
    public string ExtractedAddress { get; set; } = string.Empty;
    public DateOnly? ExtractedFromDate { get; set; }
    public DateOnly? ExtractedToDate { get; set; }
    public decimal ParserConfidence { get; set; }
    public string ProcessingStatus { get; set; } = "processed";
    public DateTime UploadedAt { get; set; }
}
