namespace LeaseLense.Domain.Entities;

public sealed class NegotiationSuggestion
{
    public Guid NegotiationSuggestionId { get; set; }
    public Guid NegotiationSessionId { get; set; }
    public string SuggestionType { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string Content { get; set; } = string.Empty;
}
