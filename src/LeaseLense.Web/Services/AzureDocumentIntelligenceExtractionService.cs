using System.Text.RegularExpressions;
using Azure;
using Azure.AI.DocumentIntelligence;
using LeaseLense.Application.Profile;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Services;

public sealed class AzureDocumentIntelligenceExtractionService : IDocumentExtractionService
{
    private static readonly Regex DateRegex = new(@"\b(20\d{2}|19\d{2})[-/](0?[1-9]|1[0-2])[-/](0?[1-9]|[12]\d|3[01])\b", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNumericRegex = new("[^A-Z0-9]", RegexOptions.Compiled);
    private static readonly string[] NameStopWords =
    [
        "CHASE", "BANK", "NATIONAL", "STATEMENT", "ACCOUNT", "PAYMENT", "TOTAL", "BALANCE", "DEPOSIT", "WITHDRAWAL"
    ];
    private readonly AzureDocumentIntelligenceOptions _options;
    private readonly IAddressExtractionLlmClient _llmClient;
    private readonly ILogger<AzureDocumentIntelligenceExtractionService> _logger;

    public AzureDocumentIntelligenceExtractionService(
        IOptions<AzureDocumentIntelligenceOptions> options,
        IAddressExtractionLlmClient llmClient,
        ILogger<AzureDocumentIntelligenceExtractionService> logger)
    {
        _options = options.Value;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<ResidencyDocumentExtractionDto> ExtractResidencyEvidenceAsync(
        byte[] fileBytes,
        string documentType,
        string renterDisplayName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var primary = await ExtractPrimaryAsync(fileBytes, documentType, renterDisplayName, contentType, cancellationToken);
        if (!primary.RequiresBackgroundFallback)
        {
            return primary.Extraction;
        }

        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Azure document extraction is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Azure document extraction configuration is incomplete.");
        }

        var client = new DocumentIntelligenceClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey));
        var layoutAndLlm = await TryExtractByLayoutAndLlmAsync(client, fileBytes, documentType, cancellationToken);
        if (layoutAndLlm is null)
        {
            _logger.LogWarning(
                "Using primary extraction because layout+LLM fallback returned null. DocumentType: {DocumentType}",
                documentType);
            return SanitizePrimaryFallbackExtraction(documentType, primary.Extraction);
        }

        _logger.LogInformation(
            "Layout+LLM fallback succeeded. DocumentType: {DocumentType}, TenantCount: {TenantCount}, AddressPresent: {AddressPresent}",
            documentType,
            layoutAndLlm.Tenants.Count,
            !string.IsNullOrWhiteSpace(layoutAndLlm.Address));

        var resolvedName = ResolveExtractedName(
            renterDisplayName,
            primary.Extraction.ExtractedName,
            [],
            layoutAndLlm.Tenants);

        return new ResidencyDocumentExtractionDto
        {
            ExtractedName = resolvedName,
            ExtractedAddress = FirstNonEmpty(layoutAndLlm.Address, primary.Extraction.ExtractedAddress) ?? string.Empty,
            ExtractedFromDate = primary.Extraction.ExtractedFromDate,
            ExtractedToDate = primary.Extraction.ExtractedToDate,
            ParserConfidence = layoutAndLlm.Confidence,
            RawText = primary.Extraction.RawText
        };
    }

    public async Task<PrimaryResidencyExtractionResult> ExtractPrimaryAsync(
        byte[] fileBytes,
        string documentType,
        string renterDisplayName,
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

        _ = contentType;
        AnalyzeResult primaryResult;
        var modelUsed = ResolveModelForDocumentType(documentType);
        var client = new DocumentIntelligenceClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey));
        try
        {
            primaryResult = await AnalyzeWithModelAsync(client, fileBytes, modelUsed, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && !string.Equals(modelUsed, "prebuilt-layout", StringComparison.OrdinalIgnoreCase))
        {
            primaryResult = await AnalyzeWithModelAsync(client, fileBytes, _options.LayoutFallbackModelId, cancellationToken);
        }

        var content = primaryResult.Content ?? string.Empty;
        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var (structuredName, structuredAddress) = ExtractStructuredNameAddress(primaryResult, documentType);
        var extractedName = ResolveExtractedName(renterDisplayName, structuredName, lines, null);
        var extractedAddress = FirstNonEmpty(structuredAddress, ExtractLikelyAddress(lines)) ?? string.Empty;
        var dates = DateRegex.Matches(content).Select(x => x.Value).Distinct().Take(2).ToList();

        DateOnly? fromDate = ParseDate(dates.ElementAtOrDefault(0));
        DateOnly? toDate = ParseDate(dates.ElementAtOrDefault(1));
        var parserConfidence = primaryResult.Documents.Count > 0
            ? Convert.ToDecimal(primaryResult.Documents.Average(x => x.Confidence))
            : 0m;

        var extraction = new ResidencyDocumentExtractionDto
        {
            ExtractedName = extractedName,
            ExtractedAddress = extractedAddress,
            ExtractedFromDate = fromDate,
            ExtractedToDate = toDate,
            ParserConfidence = parserConfidence,
            RawText = content
        };

        var requiresBackgroundFallback = RequiresFallback(documentType, extraction);

        return new PrimaryResidencyExtractionResult
        {
            Extraction = extraction,
            RequiresBackgroundFallback = requiresBackgroundFallback
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

    private bool RequiresFallback(string documentType, ResidencyDocumentExtractionDto extraction)
    {
        if (!_options.BackgroundFallbackEnabled)
        {
            return false;
        }

        var normalizedDocType = (documentType ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(normalizedDocType, "lease", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var missingData = string.IsNullOrWhiteSpace(extraction.ExtractedName) || string.IsNullOrWhiteSpace(extraction.ExtractedAddress);
        return extraction.ParserConfidence < _options.StructuredFallbackParserConfidenceThreshold || missingData;
    }

    private async Task<AddressExtractionLlmResult?> TryExtractByLayoutAndLlmAsync(
        DocumentIntelligenceClient client,
        byte[] fileBytes,
        string documentType,
        CancellationToken cancellationToken)
    {
        try
        {
            var layoutResult = await AnalyzeWithModelAsync(client, fileBytes, _options.LayoutFallbackModelId, cancellationToken);
            var layoutText = layoutResult.Content ?? string.Empty;
            var llmResult = await _llmClient.TryExtractAsync(layoutText, documentType, cancellationToken);
            return llmResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Layout+LLM extraction fallback failed.");
            return null;
        }
    }

    private static string ResolveExtractedName(
        string renterDisplayName,
        string? structuredName,
        IReadOnlyList<string> lines,
        IReadOnlyList<string>? llmTenants)
    {
        if (IsTenantIncluded(renterDisplayName, llmTenants))
        {
            return renterDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(structuredName)
            && structuredName.Contains(renterDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return renterDisplayName.Trim();
        }

        var likelyName = ExtractLikelyName(lines);
        if (!string.IsNullOrWhiteSpace(likelyName)
            && likelyName.Contains(renterDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return renterDisplayName.Trim();
        }

        return string.Empty;
    }

    private static bool IsTenantIncluded(string renterDisplayName, IReadOnlyList<string>? tenants)
    {
        if (string.IsNullOrWhiteSpace(renterDisplayName) || tenants is null || tenants.Count == 0)
        {
            return false;
        }

        var normalizedRenter = NormalizeForNameMatch(renterDisplayName);
        return tenants.Any(x =>
            !string.IsNullOrWhiteSpace(x)
            && IsNameNearMatch(normalizedRenter, NormalizeForNameMatch(x)));
    }

    private static ResidencyDocumentExtractionDto SanitizePrimaryFallbackExtraction(
        string documentType,
        ResidencyDocumentExtractionDto extraction)
    {
        var normalizedDocType = (documentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.Equals(normalizedDocType, "lease", StringComparison.OrdinalIgnoreCase))
        {
            return extraction;
        }

        var address = extraction.ExtractedAddress ?? string.Empty;
        var isNoisyLeaseAddress = address.Contains("THIS LEASE CONTRACT", StringComparison.OrdinalIgnoreCase)
                                  || address.Contains("SOMETIMES REFERRED TO AS", StringComparison.OrdinalIgnoreCase)
                                  || address.Contains("RESIDENT(S)", StringComparison.OrdinalIgnoreCase);

        if (!isNoisyLeaseAddress)
        {
            return extraction;
        }

        return new ResidencyDocumentExtractionDto
        {
            ExtractedName = extraction.ExtractedName ?? string.Empty,
            ExtractedAddress = string.Empty,
            ExtractedFromDate = extraction.ExtractedFromDate,
            ExtractedToDate = extraction.ExtractedToDate,
            ParserConfidence = extraction.ParserConfidence,
            RawText = extraction.RawText
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
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

    private static string NormalizeForNameMatch(string value)
    {
        var upper = value.ToUpperInvariant();
        var cleaned = NonAlphaNumericRegex.Replace(upper, " ");
        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsNameNearMatch(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        if (actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
            || expected.Contains(actual, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedTokens = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var actualTokens = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (expectedTokens.Length == 0 || actualTokens.Length == 0)
        {
            return false;
        }

        var matched = 0;
        foreach (var token in expectedTokens)
        {
            if (actualTokens.Any(candidate => IsTokenNearMatch(token, candidate)))
            {
                matched++;
            }
        }

        return (decimal)matched / expectedTokens.Length >= 0.67m;
    }

    private static bool IsTokenNearMatch(string expectedToken, string actualToken)
    {
        var distance = ComputeLevenshteinDistance(expectedToken, actualToken);
        return distance <= 4;
    }

    private static int ComputeLevenshteinDistance(string left, string right)
    {
        if (left.Length == 0) return right.Length;
        if (right.Length == 0) return left.Length;

        var dp = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[left.Length, right.Length];
    }
}
