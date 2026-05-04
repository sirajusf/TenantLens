using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LeaseLense.Application.Search;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Services;

/// <summary>
/// Calls Azure AI Foundry to parse a user's natural-language search query into
/// a structured <see cref="SearchQueryDto"/> using a compact system prompt.
/// Reuses the same Foundry options as the address-extraction client.
/// </summary>
public sealed class AzureFoundryNlQueryParserClient : INlQueryParserLlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string SystemPrompt = LoadPromptFile("nl_search_system_prompt.txt");

    private readonly HttpClient _httpClient;
    private readonly AzureDocumentIntelligenceOptions _options;
    private readonly ILogger<AzureFoundryNlQueryParserClient> _logger;

    public AzureFoundryNlQueryParserClient(
        HttpClient httpClient,
        IOptions<AzureDocumentIntelligenceOptions> options,
        ILogger<AzureFoundryNlQueryParserClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NlQueryParseResult?> TryParseAsync(string userQuery, CancellationToken cancellationToken = default)
    {
        var foundry = _options.Foundry;
        if (!foundry.Enabled
            || string.IsNullOrWhiteSpace(foundry.Endpoint)
            || string.IsNullOrWhiteSpace(foundry.ApiKey)
            || string.IsNullOrWhiteSpace(foundry.Model)
            || string.IsNullOrWhiteSpace(userQuery))
        {
            return null;
        }

        var requestUri = BuildRequestUri(foundry);
        var useResponsesApi = requestUri.AbsolutePath.Contains("/responses", StringComparison.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", foundry.ApiKey);
        request.Content = new StringContent(
            BuildRequestBody(foundry.Model, userQuery, useResponsesApi),
            Encoding.UTF8,
            "application/json");

        // NL parsing is synchronous and user-facing — keep timeout tight.
        var timeoutSeconds = Math.Max(5, Math.Min(foundry.TimeoutSeconds, 12));
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            using var response = await _httpClient.SendAsync(request, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NL query parser Foundry call failed with {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(linkedCts.Token);
            return TryParseResponse(payload, useResponsesApi, userQuery);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("NL query parser Foundry call timed out or was cancelled.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NL query parser Foundry call threw an unexpected exception.");
            return null;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static Uri BuildRequestUri(AddressExtractionFoundryOptions foundry)
    {
        var endpoint = foundry.Endpoint.Trim();
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? uri
            : new Uri("https://invalid.local/");
    }

    private static string BuildRequestBody(string model, string userQuery, bool useResponsesApi)
    {
        if (useResponsesApi)
        {
            var payload = new
            {
                model,
                input = new object[]
                {
                    new { role = "system", content = new[] { new { type = "input_text", text = SystemPrompt } } },
                    new { role = "user",   content = new[] { new { type = "input_text", text = userQuery } } }
                }
            };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        var chatPayload = new
        {
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = userQuery }
            }
        };
        return JsonSerializer.Serialize(chatPayload, JsonOptions);
    }

    private NlQueryParseResult? TryParseResponse(string payload, bool useResponsesApi, string originalQuery)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var content = useResponsesApi
                ? TryExtractResponsesOutputText(root)
                : TryExtractChatContent(root);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("NL query parser: Foundry response contained no output text.");
                return null;
            }

            var json = ExtractJsonObject(content);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("NL query parser: Could not find JSON object in Foundry output.");
                return null;
            }

            using var resultDoc = JsonDocument.Parse(json);
            var r = resultDoc.RootElement;

            var entityType = ParseEntityType(r);
            var queryText = GetString(r, "queryText") ?? originalQuery;
            var city = GetString(r, "city");
            var propertyType = GetString(r, "propertyType");
            var landlordName = GetString(r, "landlordName");
            var minRent = GetDecimal(r, "minRent");
            var maxRent = GetDecimal(r, "maxRent");
            var minRating = GetDecimal(r, "minRating");
            var verifiedOnly = GetBool(r, "verifiedOnly");
            var hasVerifiedReviews = GetBool(r, "hasVerifiedReviews");
            var scamType = GetString(r, "scamType");
            var minSeverity = GetDecimal(r, "minSeverity");
            var sortBy = GetString(r, "sortBy");
            var interpretedFilters = ParseStringArray(r, "interpretedFilters");

            var query = new SearchQueryDto
            {
                EntityType = entityType,
                QueryText = queryText,
                City = city,
                PropertyType = propertyType,
                LandlordName = landlordName,
                MinRent = minRent,
                MaxRent = maxRent,
                MinRating = minRating,
                VerifiedOnly = verifiedOnly,
                HasVerifiedReviews = hasVerifiedReviews,
                ScamType = scamType,
                MinSeverity = minSeverity,
                SortBy = sortBy,
                Limit = 40
            };

            return new NlQueryParseResult
            {
                Query = query,
                InterpretedFilters = interpretedFilters
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NL query parser: Failed to deserialize Foundry JSON response.");
            return null;
        }
    }

    private static SearchEntityType ParseEntityType(JsonElement root)
    {
        var raw = GetString(root, "entityType") ?? "Property";
        return raw.Trim().ToLowerInvariant() switch
        {
            "review" or "reviews" => SearchEntityType.Review,
            "scamreport" or "scam" or "scamreports" or "fraud" => SearchEntityType.ScamReport,
            _ => SearchEntityType.Property
        };
    }

    private static string? GetString(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
        return null;
    }

    private static decimal? GetDecimal(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
            return d;
        return null;
    }

    private static bool GetBool(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array)
            return [];
        var result = new List<string>();
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s)) result.Add(s.Trim());
            }
        }
        return result;
    }

    private static string? TryExtractResponsesOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var t) && t.GetString() == "output_text"
                    && part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString();
            }
        }
        return null;
    }

    private static string? TryExtractChatContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var msg)
                && msg.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
                return content.GetString();
        }
        return null;
    }

    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text[start..(end + 1)];
    }

    private static string LoadPromptFile(string fileName)
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            var path = Path.Combine(dir, "Prompts", fileName);
            if (File.Exists(path)) return File.ReadAllText(path);
            // fallback: look relative to the source file location
            var altPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? dir,
                "Prompts", fileName);
            return File.Exists(altPath) ? File.ReadAllText(altPath) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
