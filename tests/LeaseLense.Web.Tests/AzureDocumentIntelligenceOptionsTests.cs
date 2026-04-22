using LeaseLense.Web.Services;
using Xunit;

namespace LeaseLense.Web.Tests;

public sealed class AzureDocumentIntelligenceOptionsTests
{
    [Fact]
    public void Defaults_ShouldMatchRoutingPlan()
    {
        var options = new AzureDocumentIntelligenceOptions();

        Assert.Equal("prebuilt-bankStatement.us", options.BankStatementModelId);
        Assert.Equal("prebuilt-invoice", options.UtilityBillModelId);
        Assert.Equal("prebuilt-layout", options.LeaseModelId);
        Assert.Equal("prebuilt-layout", options.LayoutFallbackModelId);
    }
}
