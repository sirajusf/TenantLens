using LeaseLense.Application.Profile;

namespace LeaseLense.Web.Services;

public interface IDocumentExtractionService
{
    Task<DocumentOcrTextResult> ExtractOcrTextAsync(
        byte[] fileBytes,
        string documentType,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<PrimaryResidencyExtractionResult> ExtractPrimaryAsync(
        byte[] fileBytes,
        string documentType,
        string renterDisplayName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<ResidencyDocumentExtractionDto> ExtractResidencyEvidenceAsync(
        byte[] fileBytes,
        string documentType,
        string renterDisplayName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed class PrimaryResidencyExtractionResult
{
    public ResidencyDocumentExtractionDto Extraction { get; init; } = new();
    public bool RequiresBackgroundFallback { get; init; }
}

public sealed class DocumentOcrTextResult
{
    public string RawText { get; init; } = string.Empty;
    public string ModelUsed { get; init; } = string.Empty;
}
