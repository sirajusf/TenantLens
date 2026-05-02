using LeaseLense.Application.ScamReports;

namespace LeaseLense.Application.Abstractions;

public interface IScamReportMvpService
{
    Task<IReadOnlyList<ScamReportListItemDto>> GetScamReportsAsync(
        string? queryText = null,
        string? city = null,
        decimal? minSeverity = null,
        string? scamType = null,
        decimal? maxSeverity = null,
        DateOnly? dateReportedAfter = null,
        string? sortBy = null,
        CancellationToken cancellationToken = default);

    Task<ScamReportFormMetadataDto> GetFormMetadataAsync(
        string reporterEmail,
        CancellationToken cancellationToken = default);

    Task SubmitScamReportAsync(
        CreateScamReportDto request,
        CancellationToken cancellationToken = default);
}
