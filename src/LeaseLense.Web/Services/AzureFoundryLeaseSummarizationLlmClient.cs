using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Services;

public sealed class AzureFoundryLeaseSummarizationLlmClient : ILeaseSummarizationLlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string LeaseSummarizerSystemPrompt = LoadPromptFile("lease_summarizer_system_prompt.txt");

    private readonly HttpClient _httpClient;
    private readonly AzureDocumentIntelligenceOptions _options;
    private readonly ILogger<AzureFoundryLeaseSummarizationLlmClient> _logger;
    private readonly ILlmFoundryErrorFileLog _errorFileLog;
    private readonly LlmFoundryFileLoggingOptions _fileLogOptions;

    public AzureFoundryLeaseSummarizationLlmClient(
        HttpClient httpClient,
        IOptions<AzureDocumentIntelligenceOptions> options,
        IOptions<LlmFoundryFileLoggingOptions> fileLoggingOptions,
        ILogger<AzureFoundryLeaseSummarizationLlmClient> logger,
        ILlmFoundryErrorFileLog errorFileLog)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fileLogOptions = fileLoggingOptions.Value;
        _logger = logger;
        _errorFileLog = errorFileLog;
    }

    public async Task<LeaseSummarizationLlmResult?> TrySummarizeAsync(string ocrText, CancellationToken cancellationToken = default)
    {
        var foundry = _options.Foundry;
        if (!foundry.Enabled
            || string.IsNullOrWhiteSpace(foundry.Endpoint)
            || string.IsNullOrWhiteSpace(foundry.ApiKey)
            || string.IsNullOrWhiteSpace(foundry.Model)
            || string.IsNullOrWhiteSpace(ocrText))
        {
            return null;
        }

        var requestUri = BuildRequestUri(foundry);
        var useResponsesApi = IsResponsesApiEndpoint(requestUri);
        var maxRetries = Math.Max(0, foundry.MaxRetries);
        var maxAttempts = maxRetries + 1;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", foundry.ApiKey);
            request.Content = new StringContent(
                BuildRequestBody(foundry.Model, ocrText, useResponsesApi),
                Encoding.UTF8,
                "application/json");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, foundry.TimeoutSeconds)));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                using var response = await _httpClient.SendAsync(request, linkedCts.Token);
                var payload = await response.Content.ReadAsStringAsync(linkedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    TryWriteFoundryErrorFile(
                        response.StatusCode >= System.Net.HttpStatusCode.InternalServerError || (int)response.StatusCode == 429
                            ? LlmFoundryFileLogSeverity.Error
                            : LlmFoundryFileLogSeverity.Warning,
                        "FoundryHttpError",
                        "Foundry returned a non-success HTTP status for lease summarization.",
                        context: new Dictionary<string, string?>
                        {
                            ["attempt"] = (attempt + 1).ToString(),
                            ["maxAttempts"] = maxAttempts.ToString(),
                            ["statusCode"] = ((int)response.StatusCode).ToString(),
                            ["reasonPhrase"] = response.ReasonPhrase,
                            ["requestPath"] = SafeLogUriPath(requestUri),
                            ["useResponsesApi"] = useResponsesApi ? "true" : "false",
                            ["errorBodyExcerpt"] = Truncate(payload, _fileLogOptions.ExcerptMaxLength)
                        });

                    continue;
                }

                var parsed = TryParseResult(payload, useResponsesApi, out var parseError, out var parseException);
                if (parsed is not null)
                {
                    return parsed;
                }

                TryWriteFoundryErrorFile(
                    parseException is null ? LlmFoundryFileLogSeverity.Warning : LlmFoundryFileLogSeverity.Error,
                    parseException is null ? "FoundryResponseParseInvalid" : "FoundryResponseParseException",
                    parseError ?? "Lease summarizer response could not be parsed into the expected JSON.",
                    parseException,
                    new Dictionary<string, string?>
                    {
                        ["attempt"] = (attempt + 1).ToString(),
                        ["maxAttempts"] = maxAttempts.ToString(),
                        ["useResponsesApi"] = useResponsesApi ? "true" : "false",
                        ["requestPath"] = SafeLogUriPath(requestUri),
                        ["payloadExcerpt"] = Truncate(payload, _fileLogOptions.ExcerptMaxLength)
                    });
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                TryWriteFoundryErrorFile(
                    LlmFoundryFileLogSeverity.Warning,
                    "FoundryRequestCancelled",
                    "Lease summarization was cancelled (caller requested cancellation).",
                    ex,
                    new Dictionary<string, string?>
                    {
                        ["attempt"] = (attempt + 1).ToString(),
                        ["maxAttempts"] = maxAttempts.ToString(),
                        ["requestPath"] = SafeLogUriPath(requestUri)
                    });
                throw;
            }
            catch (OperationCanceledException ex)
            {
                TryWriteFoundryErrorFile(
                    LlmFoundryFileLogSeverity.Error,
                    "FoundryRequestTimeout",
                    "Foundry request timed out or the operation was cancelled before completion.",
                    ex,
                    new Dictionary<string, string?>
                    {
                        ["attempt"] = (attempt + 1).ToString(),
                        ["maxAttempts"] = maxAttempts.ToString(),
                        ["requestPath"] = SafeLogUriPath(requestUri)
                    });
            }
            catch (Exception ex)
            {
                TryWriteFoundryErrorFile(
                    LlmFoundryFileLogSeverity.Error,
                    "FoundryRequestException",
                    "Unhandled exception while calling Foundry for lease summarization.",
                    ex,
                    new Dictionary<string, string?>
                    {
                        ["attempt"] = (attempt + 1).ToString(),
                        ["maxAttempts"] = maxAttempts.ToString(),
                        ["requestPath"] = SafeLogUriPath(requestUri)
                    });
            }

            try
            {
                _logger.LogWarning("Lease summarization attempt {Attempt}/{MaxAttempts} failed.", attempt + 1, maxAttempts);
            }
            catch
            {
            }
        }

        return null;
    }

    private static Uri BuildRequestUri(AddressExtractionFoundryOptions foundry)
    {
        var endpoint = foundry.Endpoint.Trim();
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? uri
            : new Uri("https://invalid.local/");
    }

    private static bool IsResponsesApiEndpoint(Uri requestUri)
    {
        return requestUri.AbsolutePath.Contains("/responses", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRequestBody(string model, string ocrText, bool useResponsesApi)
    {
        if (useResponsesApi)
        {
            var payload = new
            {
                model,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new[] { new { type = "input_text", text = LeaseSummarizerSystemPrompt } }
                    },
                    new
                    {
                        role = "user",
                        content = new[] { new { type = "input_text", text = ocrText } }
                    }
                }
            };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        var chatPayload = new
        {
            messages = new object[]
            {
                new { role = "system", content = LeaseSummarizerSystemPrompt },
                new { role = "user", content = ocrText }
            }
        };
        return JsonSerializer.Serialize(chatPayload, JsonOptions);
    }

    private static LeaseSummarizationLlmResult? TryParseResult(
        string payload,
        bool useResponsesApi,
        out string? error,
        out Exception? exception)
    {
        error = null;
        exception = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var content = useResponsesApi
                ? TryExtractResponsesOutputText(root)
                : TryExtractChatCompletionContent(root);

            if (string.IsNullOrWhiteSpace(content))
            {
                error = "Foundry response did not include output text.";
                return null;
            }

            var json = ExtractJsonObject(content);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Foundry output did not contain a JSON object.";
                return null;
            }

            using var resultDoc = JsonDocument.Parse(json);
            var resultRoot = resultDoc.RootElement;

            var summaryText = resultRoot.TryGetProperty("summaryText", out var summary) && summary.ValueKind == JsonValueKind.String
                ? summary.GetString() ?? string.Empty
                : string.Empty;

            var confidence = ParseConfidence(resultRoot);

            decimal? riskScore = null;
            if (resultRoot.TryGetProperty("summaryRiskScore", out var riskScoreEl))
            {
                if (riskScoreEl.ValueKind == JsonValueKind.Number && riskScoreEl.TryGetDecimal(out var d))
                {
                    riskScore = d;
                }
            }

            string? riskLevel = null;
            if (resultRoot.TryGetProperty("riskLevel", out var riskLevelEl) && riskLevelEl.ValueKind == JsonValueKind.String)
            {
                riskLevel = riskLevelEl.GetString();
            }

            var flags = new List<LeaseClauseFlagDto>();
            if (resultRoot.TryGetProperty("clauseFlags", out var flagsEl) && flagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in flagsEl.EnumerateArray())
                {
                    flags.Add(new LeaseClauseFlagDto
                    {
                        ClauseType = item.TryGetProperty("clauseType", out var ct) && ct.ValueKind == JsonValueKind.String ? ct.GetString() ?? string.Empty : string.Empty,
                        RiskLevel = item.TryGetProperty("riskLevel", out var rl) && rl.ValueKind == JsonValueKind.String ? rl.GetString() ?? string.Empty : string.Empty,
                        FlaggedText = item.TryGetProperty("flaggedText", out var ft) && ft.ValueKind == JsonValueKind.String ? ft.GetString() ?? string.Empty : string.Empty,
                        Explanation = item.TryGetProperty("explanation", out var ex) && ex.ValueKind == JsonValueKind.String ? ex.GetString() ?? string.Empty : string.Empty,
                        SuggestedQuestion = item.TryGetProperty("suggestedQuestion", out var sq) && sq.ValueKind == JsonValueKind.String ? sq.GetString() ?? string.Empty : string.Empty
                    });
                }
            }

            return new LeaseSummarizationLlmResult
            {
                SummaryText = summaryText,
                Confidence = confidence,
                SummaryRiskScore = riskScore,
                RiskLevel = string.IsNullOrWhiteSpace(riskLevel) ? null : riskLevel,
                ClauseFlags = flags,
                RawJson = json
            };
        }
        catch (Exception ex)
        {
            exception = ex;
            error = ex.Message;
            return null;
        }
    }

    private void TryWriteFoundryErrorFile(
        LlmFoundryFileLogSeverity severity,
        string eventName,
        string message,
        Exception? exception = null,
        Dictionary<string, string?>? context = null)
    {
        try
        {
            _errorFileLog.Write(severity, eventName, message, exception, context);
        }
        catch
        {
        }
    }

    private static string? TryExtractChatCompletionContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var msg)
                && msg.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }
        }

        return null;
    }

    private static string? TryExtractResponsesOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var content in contentArray.EnumerateArray())
            {
                if (content.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && string.Equals(type.GetString(), "output_text", StringComparison.OrdinalIgnoreCase)
                    && content.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static string LoadPromptFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", fileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Prompt file not found: {path}");
        }

        return File.ReadAllText(path);
    }

    private static string? ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed[firstBrace..(lastBrace + 1)];
            }
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return null;
    }

    private static string SafeLogUriPath(Uri uri)
    {
        try
        {
            return uri.AbsolutePath;
        }
        catch
        {
            return "(unavailable)";
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private static decimal ParseConfidence(JsonElement root)
    {
        const decimal defaultConfidence = 0.5m;
        if (!root.TryGetProperty("confidence", out var confidenceElement))
        {
            return defaultConfidence;
        }

        decimal value;
        switch (confidenceElement.ValueKind)
        {
            case JsonValueKind.Number:
                if (!confidenceElement.TryGetDecimal(out value))
                {
                    return defaultConfidence;
                }
                break;
            case JsonValueKind.String:
                if (!decimal.TryParse(confidenceElement.GetString(), out value))
                {
                    return defaultConfidence;
                }
                break;
            default:
                return defaultConfidence;
        }

        return Math.Clamp(value, 0m, 1m);
    }
}

