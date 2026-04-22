using System.Reflection;
using LeaseLense.Application.Profile;
using LeaseLense.Web.Services;
using Xunit;

namespace LeaseLense.Web.Tests;

public sealed class AzureDocumentIntelligenceExtractionServiceFallbackTests
{
    [Fact]
    public void LeaseFallbackFailure_ClearsBoilerplateAddress()
    {
        var primary = new ResidencyDocumentExtractionDto
        {
            ExtractedName = "Mahammad Siraj Cherun",
            ExtractedAddress = "1. PARTIES. THIS LEASE CONTRACT (SOMETIMES REFERRED TO AS THE LEASE)...",
            ParserConfidence = 0.21m
        };

        var method = typeof(AzureDocumentIntelligenceExtractionService)
            .GetMethod("SanitizePrimaryFallbackExtraction", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var sanitized = (ResidencyDocumentExtractionDto)method!.Invoke(null, ["lease", primary])!;

        Assert.Equal(primary.ExtractedName, sanitized.ExtractedName);
        Assert.Equal(string.Empty, sanitized.ExtractedAddress);
    }

    [Fact]
    public void TenantMatch_AllowsNearNameTypos()
    {
        var method = typeof(AzureDocumentIntelligenceExtractionService)
            .GetMethod("IsTenantIncluded", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var matched = (bool)method!.Invoke(null, ["Mahammad Siraj Cheruvu", new[] { "Mahammad Siraj Cherun" }])!;

        Assert.True(matched);
    }
}
