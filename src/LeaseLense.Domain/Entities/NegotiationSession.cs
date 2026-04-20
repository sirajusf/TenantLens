namespace LeaseLense.Domain.Entities;

public sealed class NegotiationSession
{
    public Guid NegotiationSessionId { get; set; }
    public Guid RenterId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid? LeaseAnalysisId { get; set; }
    public DateTime CreatedAt { get; set; }
}
