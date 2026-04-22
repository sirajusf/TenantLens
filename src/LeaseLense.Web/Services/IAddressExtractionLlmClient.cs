namespace LeaseLense.Web.Services;

public interface IAddressExtractionLlmClient
{
    Task<AddressExtractionLlmResult?> TryExtractAsync(
        string ocrText,
        string documentType,
        CancellationToken cancellationToken = default);
}

public sealed class AddressExtractionLlmResult
{
    public IReadOnlyList<string> Tenants { get; init; } = [];
    public string Address { get; init; } = string.Empty;
    public decimal Confidence { get; init; } = 0.5m;
}
