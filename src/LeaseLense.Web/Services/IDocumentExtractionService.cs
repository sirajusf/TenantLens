using LeaseLense.Application.Profile;

namespace LeaseLense.Web.Services;

public interface IDocumentExtractionService
{
    Task<ResidencyDocumentExtractionDto> ExtractResidencyEvidenceAsync(
        byte[] fileBytes,
        string documentType,
        string contentType,
        CancellationToken cancellationToken = default);
}
