namespace LeaseLense.Domain.Entities;

public sealed class LeaseSummarizationJob
{
    public Guid LeaseSummarizationJobId { get; set; }
    public Guid LeaseDocumentId { get; set; }
    public Guid RenterId { get; set; }

    public string Status { get; set; } = "queued";
    public string? ErrorMessage { get; set; }

    public Guid? LeaseAnalysisId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

