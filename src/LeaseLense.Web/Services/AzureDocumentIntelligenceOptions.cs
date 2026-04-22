namespace LeaseLense.Web.Services;

public sealed class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "prebuilt-document";
    public string LeaseModelId { get; set; } = "prebuilt-document";
    public string UtilityBillModelId { get; set; } = "prebuilt-layout";
    public string BankStatementModelId { get; set; } = "prebuilt-bankStatement.us";
}
