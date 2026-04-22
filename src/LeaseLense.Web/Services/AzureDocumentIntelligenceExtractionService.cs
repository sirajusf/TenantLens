using System.Diagnostics;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.DocumentIntelligence;
using LeaseLense.Application.Profile;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Services;

public sealed class AzureDocumentIntelligenceExtractionService : IDocumentExtractionService
{
    private static readonly Regex DateRegex = new(@"\b(20\d{2}|19\d{2})[-/](0?[1-9]|1[0-2])[-/](0?[1-9]|[12]\d|3[01])\b", RegexOptions.Compiled);
    private static readonly string[] NameStopWords =
    [
        "CHASE", "BANK", "NATIONAL", "STATEMENT", "ACCOUNT", "PAYMENT", "TOTAL", "BALANCE", "DEPOSIT", "WITHDRAWAL"
    ];
    private readonly AzureDocumentIntelligenceOptions _options;

    public AzureDocumentIntelligenceExtractionService(IOptions<AzureDocumentIntelligenceOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ResidencyDocumentExtractionDto> ExtractResidencyEvidenceAsync(
        byte[] fileBytes,
        string documentType,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Azure document extraction is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Azure document extraction configuration is incomplete.");
        }

        var sw = Stopwatch.StartNew();
        AnalyzeResult result;
        var modelUsed = ResolveModelForDocumentType(documentType);
        var client = new DocumentIntelligenceClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey));
        try
        {
            result = await AnalyzeWithModelAsync(client, fileBytes, modelUsed, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && !string.Equals(modelUsed, "prebuilt-layout", StringComparison.OrdinalIgnoreCase))
        {
            modelUsed = "prebuilt-layout";
            result = await AnalyzeWithModelAsync(client, fileBytes, modelUsed, cancellationToken);
        }

        sw.Stop();
        var content = result.Content ?? string.Empty;
        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var (structuredName, structuredAddress) = ExtractStructuredNameAddress(result, documentType);
        var extractedName = structuredName ?? ExtractLikelyName(lines) ?? string.Empty;
        var extractedAddress = structuredAddress ?? ExtractLikelyAddress(lines) ?? string.Empty;
        var dates = DateRegex.Matches(content).Select(x => x.Value).Distinct().Take(2).ToList();

        DateOnly? fromDate = ParseDate(dates.ElementAtOrDefault(0));
        DateOnly? toDate = ParseDate(dates.ElementAtOrDefault(1));
        var parserConfidence = result.Documents.Count > 0
            ? Convert.ToDecimal(result.Documents.Average(x => x.Confidence))
            : 0m;

        return new ResidencyDocumentExtractionDto
        {
            ExtractedName = extractedName,
            ExtractedAddress = extractedAddress,
            ExtractedFromDate = fromDate,
            ExtractedToDate = toDate,
            ParserConfidence = parserConfidence,
            RawText = content
        };
    }

    private static async Task<AnalyzeResult> AnalyzeWithModelAsync(
        DocumentIntelligenceClient client,
        byte[] fileBytes,
        string modelId,
        CancellationToken cancellationToken)
    {
        var operation = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            modelId,
            BinaryData.FromBytes(fileBytes),
            cancellationToken: cancellationToken);
        return operation.Value;
    }

    private string ResolveModelForDocumentType(string? documentType)
    {
        var normalized = (documentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "bank_statement" => _options.BankStatementModelId,
            "utility_bill" => _options.UtilityBillModelId,
            "lease" => _options.LeaseModelId,
            _ => _options.ModelId
        };
    }

    private static bool IsLikelyName(string line)
    {
        var upper = line.ToUpperInvariant();
        return upper.Length >= 5
               && upper.Any(char.IsLetter)
               && !upper.Any(char.IsDigit)
               && !upper.Contains("LEASE")
               && !upper.Contains("STATEMENT")
               && !upper.Contains("UTILITY");
    }

    private static string? ExtractLikelyName(IReadOnlyList<string> lines)
    {
        // Try to parse "NAME + STREET NUMBER ..." lines often seen in bank statements.
        var inlineNameRegex = new Regex(@"([A-Z][A-Z'\-]+(?:\s+[A-Z][A-Z'\-]+){1,3})\s+\d{1,6}\s+", RegexOptions.Compiled);
        foreach (var raw in lines)
        {
            var upper = raw.ToUpperInvariant();
            var match = inlineNameRegex.Match(upper);
            if (!match.Success)
            {
                continue;
            }

            var candidate = match.Groups[1].Value.Trim();
            if (!NameStopWords.Any(word => candidate.Contains(word, StringComparison.Ordinal)))
            {
                return candidate;
            }
        }

        // Fallback to alpha-only candidate lines.
        return lines
            .Select(x => x.Trim())
            .FirstOrDefault(IsLikelyName);
    }

    private static (string? Name, string? Address) ExtractStructuredNameAddress(AnalyzeResult result, string? documentType)
    {
        if (result.Documents.Count == 0)
        {
            return (null, null);
        }

        var normalizedDocType = (documentType ?? string.Empty).Trim().ToLowerInvariant();
        var nameKeys = normalizedDocType switch
        {
            "bank_statement" => new[] { "AccountHolderName", "CustomerName", "Name" },
            "utility_bill" => new[] { "CustomerName", "AccountHolderName", "Name", "ServiceAddress" },
            "lease" => new[] { "TenantName", "LesseeName", "RenterName", "CustomerName", "Name" },
            _ => new[] { "Name", "CustomerName", "AccountHolderName", "TenantName", "LesseeName" }
        };

        var addressKeys = normalizedDocType switch
        {
            "bank_statement" => new[] { "AccountHolderAddress", "CustomerAddress", "Address" },
            "utility_bill" => new[] { "ServiceAddress", "BillingAddress", "CustomerAddress", "Address" },
            "lease" => new[] { "PropertyAddress", "ServiceAddress", "TenantAddress", "Address" },
            _ => new[] { "Address", "ServiceAddress", "CustomerAddress", "PropertyAddress" }
        };

        foreach (var doc in result.Documents)
        {
            if (doc.Fields is null || doc.Fields.Count == 0)
            {
                continue;
            }

            var name = TryGetFieldContent(doc.Fields, nameKeys);
            var address = TryGetFieldContent(doc.Fields, addressKeys);
            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(address))
            {
                return (name, address);
            }
        }

        return (null, null);
    }

    private static string? TryGetFieldContent(IReadOnlyDictionary<string, DocumentField> fields, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            var pair = fields.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(pair.Key))
            {
                continue;
            }

            var field = pair.Value;
            if (!string.IsNullOrWhiteSpace(field.ValueString))
            {
                return field.ValueString.Trim();
            }

            if (!string.IsNullOrWhiteSpace(field.Content))
            {
                return field.Content.Trim();
            }
        }

        return null;
    }

    private static bool IsLikelyAddress(string line)
    {
        var upper = line.ToUpperInvariant();
        return upper.Any(char.IsDigit)
               && (upper.Contains("ST") || upper.Contains("AVE") || upper.Contains("DR") || upper.Contains("BLVD")
                   || upper.Contains("RD") || upper.Contains("LN") || upper.Contains("CT") || upper.Contains("WAY"));
    }

    private static string? ExtractLikelyAddress(IReadOnlyList<string> lines)
    {
        foreach (var raw in lines)
        {
            var upper = raw.ToUpperInvariant();
            if (!IsLikelyAddress(upper))
            {
                continue;
            }

            // Trim statement noise before the street number.
            var streetStart = Regex.Match(upper, @"\b\d{1,6}\s+[A-Z0-9#\-\s]{3,}");
            var candidate = streetStart.Success ? upper[streetStart.Index..] : upper;
            candidate = Regex.Replace(candidate, @"\s+", " ").Trim();
            if (candidate.Length >= 8)
            {
                return candidate;
            }
        }

        return null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value.Replace('/', '-'), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
