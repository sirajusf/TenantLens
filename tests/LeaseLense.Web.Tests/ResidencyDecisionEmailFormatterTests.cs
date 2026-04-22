using LeaseLense.Web.Services;
using Xunit;

namespace LeaseLense.Web.Tests;

public sealed class ResidencyDecisionEmailFormatterTests
{
    [Fact]
    public void BuildInProcessPlainText_ReturnsExpectedMessage()
    {
        var body = ResidencyDecisionEmailFormatter.BuildInProcessPlainText();
        Assert.Contains("Thanks for submitting your residency document", body);
        Assert.Contains("processing your verification", body);
    }

    [Fact]
    public void BuildResidencyDecisionPlainText_IncludesCustomerSummary_ForPending()
    {
        var body = ResidencyDecisionEmailFormatter.BuildResidencyDecisionPlainText(new ResidencyDecisionEmailContext
        {
            Status = "pending_manual_review",
            Reason = "Auto-checks were inconclusive; queued for manual review.",
            DisplayName = "Alex Doe",
            StreetAddress = "123 Main St",
            City = "Tampa",
            StateOrRegion = "FL",
            PostalCode = "33620",
            Country = "USA",
            DocumentType = "utility_bill",
            CommunityName = "USF Oaks",
            PropertyTitle = "Unit 402",
            FileName = "bill.pdf",
            ExtractedName = "Alex Doe",
            ExtractedAddress = "123 Main St, Tampa, FL",
            ExtractedFromDate = new DateOnly(2026, 1, 1),
            ExtractedToDate = new DateOnly(2026, 1, 31),
            ParserConfidence = 0.82m
        });

        Assert.Contains("Pending review", body);
        Assert.Contains("Name on your account: Alex Doe", body);
        Assert.Contains("123 Main St", body);
        Assert.Contains("Tampa", body);
        Assert.Contains("Alex Doe", body);
        Assert.Contains("123 Main St, Tampa, FL", body);
        Assert.DoesNotContain("Parser confidence", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bill.pdf", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("utility_bill", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildResidencyDecisionPlainText_IncludesCustomerSummary_ForRejected()
    {
        var body = ResidencyDecisionEmailFormatter.BuildResidencyDecisionPlainText(new ResidencyDecisionEmailContext
        {
            Status = "rejected",
            Reason = "Rejected due to low confidence on name/address match.",
            DisplayName = "Alex Doe",
            StreetAddress = "123 Main St",
            City = "Tampa",
            StateOrRegion = "FL",
            PostalCode = "33620",
            Country = "USA",
            DocumentType = "lease",
            CommunityName = "USF Oaks",
            PropertyTitle = "Unit 402",
            FileName = "lease.pdf",
            ExtractedName = "Not found",
            ExtractedAddress = "123 Main St, Tampa, FL",
            ExtractedFromDate = null,
            ExtractedToDate = null,
            ParserConfidence = 0.46m
        });

        Assert.Contains("Rejected", body);
        Assert.Contains("could not confirm your residency", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lease.pdf", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildEmailVerificationPlainText_IncludesUrl()
    {
        var body = ResidencyDecisionEmailFormatter.BuildEmailVerificationPlainText("https://example.com/verify");
        Assert.Contains("https://example.com/verify", body);
        Assert.Contains("LeaseLense", body);
    }

    [Fact]
    public void BuildResidencyDecisionHtml_ContainsBrandingAndSummary()
    {
        var html = LeaseLenseEmailHtmlTemplates.BuildResidencyDecisionHtml(new ResidencyDecisionEmailContext
        {
            Status = "verified_stay",
            Reason = "Internal",
            DisplayName = "Jamie",
            StreetAddress = "1 Oak Rd",
            City = "Tampa",
            Country = "USA",
            DocumentType = "lease",
            CommunityName = "X",
            PropertyTitle = "Y",
            FileName = "z.pdf",
            ExtractedName = "Jamie",
            ExtractedAddress = "1 Oak Rd, Tampa",
            ParserConfidence = 0.9m
        });

        Assert.Contains("LeaseLense", html);
        Assert.Contains("Verified stay", html);
        Assert.Contains("Jamie", html);
        Assert.Contains("1 Oak Rd", html);
    }
}
