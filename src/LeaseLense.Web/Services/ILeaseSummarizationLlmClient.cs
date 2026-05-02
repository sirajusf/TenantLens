namespace LeaseLense.Web.Services;

public interface ILeaseSummarizationLlmClient
{
    Task<LeaseSummarizationLlmResult?> TrySummarizeAsync(string ocrText, CancellationToken cancellationToken = default);
}

public sealed class LeaseSummarizationLlmResult
{
    public string SummaryText { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public decimal? SummaryRiskScore { get; init; }
    public string? RiskLevel { get; init; }
    public IReadOnlyList<LeaseClauseFlagDto> ClauseFlags { get; init; } = [];
    public string RawJson { get; init; } = string.Empty;
}

public sealed class LeaseClauseFlagDto
{
    public string ClauseType { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public string FlaggedText { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string SuggestedQuestion { get; init; } = string.Empty;
}

