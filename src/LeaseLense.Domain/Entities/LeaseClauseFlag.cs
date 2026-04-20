namespace LeaseLense.Domain.Entities;

public sealed class LeaseClauseFlag
{
    public Guid LeaseClauseFlagId { get; set; }
    public Guid LeaseAnalysisId { get; set; }
    public string ClauseType { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string? FlaggedText { get; set; }
    public string? Explanation { get; set; }
    public string? SuggestedQuestion { get; set; }
}
