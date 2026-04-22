namespace LeaseLense.Web.Services;

public sealed class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "prebuilt-document";
    public string LeaseModelId { get; set; } = "prebuilt-layout";
    public string UtilityBillModelId { get; set; } = "prebuilt-invoice";
    public string BankStatementModelId { get; set; } = "prebuilt-bankStatement.us";
    public string LayoutFallbackModelId { get; set; } = "prebuilt-layout";
    public bool BackgroundFallbackEnabled { get; set; } = true;
    public decimal StructuredFallbackParserConfidenceThreshold { get; set; } = 0.55m;
    public AddressExtractionFoundryOptions Foundry { get; set; } = new();
}

public sealed class AddressExtractionFoundryOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-10-21";
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxRetries { get; set; } = 2;
}
