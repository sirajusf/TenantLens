namespace LeaseLense.Domain.Entities;

public sealed class LeaseDocument
{
    public Guid LeaseDocumentId { get; set; }
    public Guid RenterId { get; set; }
    public Guid PropertyId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? RawText { get; set; }
    public DateTime UploadedAt { get; set; }
}
