using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Services;

public sealed class AzureFoundryAddressExtractionLlmClient : IAddressExtractionLlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string AddressExtractionSystemPrompt = LoadPromptFile("address_extraction_system_prompt.txt");
    private static readonly string AddressExtractionRetryPrompt = LoadPromptFile("address_extraction_retry_prompt.txt");
    private readonly HttpClient _httpClient;
    private readonly AzureDocumentIntelligenceOptions _options;
    private readonly ILogger<AzureFoundryAddressExtractionLlmClient> _logger;
    private readonly ILlmFoundryErrorFileLog _errorFileLog;
    private readonly LlmFoundryFileLoggingOptions _fileLogOptions;

    public AzureFoundryAddressExtractionLlmClient(
        HttpClient httpClient,
        IOptions<AzureDocumentIntelligenceOptions> options,
        IOptions<LlmFoundryFileLoggingOptions> fileLoggingOptions,
        ILogger<AzureFoundryAddressExtractionLlmClient> logger,
        ILlmFoundryErrorFileLog errorFileLog)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fileLogOptions = fileLoggingOptions.Value;
        _logger = logger;
        _errorFileLog = errorFileLog;
    }

    public async Task<AddressExtractionLlmResult?> TryExtractAsync(
        string ocrText,
        string documentType,
        CancellationToken cancellationToken = default)
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

        var maxRetries = Math.Max(0, foundry.MaxRetries);
        var maxAttempts = maxRetries + 1;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var requestUri = BuildRequestUri(foundry);
            var useResponsesApi = IsResponsesApiEndpoint(requestUri);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", foundry.ApiKey);
            request.Content = new StringContent(BuildRequestBody(ocrText, documentType, attempt, useResponsesApi), Encoding.UTF8, "application/json");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, foundry.TimeoutSeconds)));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                using var response = await _httpClient.SendAsync(request, linkedCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
                    var excerpt = Truncate(errorBody, _fileLogOptions.ExcerptMaxLength);
                    var httpSev = response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                        || (int)response.StatusCode == 429
                        ? LlmFoundryFileLogSeverity.Error
                        : LlmFoundryFileLogSeverity.Warning;
                    TryWriteFoundryErrorFile(
                        httpSev,
                        "FoundryHttpError",
                        "Foundry returned a non-success HTTP status for address extraction.",
                        context: new Dictionary<string, string?>
                        {
                            ["documentType"] = documentType,
                            ["attempt"] = (attempt + 1).ToString(),
                            ["maxAttempts"] = maxAttempts.ToString(),
                            ["statusCode"] = ((int)response.StatusCode).ToString(),
                            ["reasonPhrase"] = response.ReasonPhrase,
                            ["requestPath"] = SafeLogUriPath(requestUri),
                            ["useResponsesApi"] = useResponsesApi ? "true" : "false",
                            ["errorBodyExcerpt"] = excerpt
                        });
                    try
                    {
                        if (httpSev == LlmFoundryFileLogSeverity.Error)
                        {
                            _logger.LogError(
                                "Foundry extraction HTTP error. Status: {StatusCode}. Attempt: {Attempt}/{MaxAttempts}. Body: {Body}",
                                (int)response.StatusCode,
                                attempt + 1,
                                maxAttempts,
                                Truncate(errorBody, 400));
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Foundry extraction HTTP failure. Status: {StatusCode}. Attempt: {Attempt}/{MaxAttempts}. Body: {Body}",
                                (int)response.StatusCode,
                                attempt + 1,
                                maxAttempts,
                                Truncate(errorBody, 400));
                        }
                    }
                    catch
                    {
                    }

                    continue;
                }

                var payload = await response.Content.ReadAsStringAsync(linkedCts.Token);
                try
                {
                    WriteRawResponseArtifact(payload, documentType, attempt + 1);
                }
                catch
                {
                    // Dev artifact only; must not affect parsing or retries.
                }

                var parsed = TryParseResult(payload, useResponsesApi, out var parseError, out var parseException);
                if (parsed is not null)
                {
                    try
                    {
                        WriteParsedResultArtifact(parsed, documentType);
                    }
                    catch
                    {
                        // Dev artifact only; must not affect returning the extraction result.
                    }

                    return parsed;
                }

                if (parseException is not null)
                {
                    TryWriteFoundryErrorFile(
                        LlmFoundryFileLogSeverity.Error,
                        "FoundryResponseParseException",
                        parseError ?? "Exception while parsing Foundry model response.",
                        parseException,
                        new Dictionary<string, string?>
                        {
                            ["documentType"] = documentType,
                            ["attempt"] = (attempt + 1).ToString(),
                            ["maxAttempts"] = maxAttempts.ToString(),
                            ["useResponsesApi"] = useResponsesApi ? "true" : "false",
                            ["requestPath"] = SafeLogUriPath(requestUri),
                            ["payloadExcerpt"] = Truncate(payload, _fileLogOptions.ExcerptMaxLength)
                        });
                }
                else
                {
                    TryWriteFoundryErrorFile(
                        LlmFoundryFileLogSeverity.Warning,
                        "FoundryResponseParseInvalid",
                        parseError ?? "Model response could not be parsed into the expected address JSON.",
                        context: new Dictionary<string, string?>
                        {
                            ["documentType"] = documentType,
                            ["attempt"] = (attempt + 1).ToString(),
                            ["maxAttempts"] = maxAttempts.ToString(),
                            ["useResponsesApi"] = useResponsesApi ? "true" : "false",
                            ["requestPath"] = SafeLogUriPath(requestUri),
                            ["payloadExcerpt"] = Truncate(payload, _fileLogOptions.ExcerptMaxLength)
                        });
                }

                try
                {
                    _logger.LogWarning(
                        "Foundry extraction parse failure. Attempt: {Attempt}/{MaxAttempts}. Error: {Error}. Payload: {Payload}",
                        attempt + 1,
                        maxAttempts,
                        parseError ?? "Unknown parsing error.",
                        Truncate(payload, 400));
                }
                catch
                {
                }
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                TryWriteFoundryErrorFile(
                    LlmFoundryFileLogSeverity.Warning,
                    "FoundryRequestCancelled",
                    "Address extraction was cancelled (caller requested cancellation).",
                    ex,
                    new Dictionary<string, string?>
                    {
                        ["documentType"] = documentType,
                        ["attempt"] = (attempt + 1).ToString(),
                        ["maxAttempts"] = maxAttempts.ToString(),
                        ["requestPath"] = SafeLogUriPath(requestUri)
                    });
                try
                {
                    _logger.LogWarning(ex, "Foundry extraction cancelled (caller). Attempt {Attempt}/{MaxAttempts}.", attempt + 1, maxAttempts);
                }
                catch
                {
                }

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
                        ["documentType"] = documentType,
                        ["attempt"] = (attempt + 1).ToString(),
                        ["maxAttempts"] = maxAttempts.ToString(),
                        ["requestPath"] = SafeLogUriPath(requestUri),
                        ["timeoutSeconds"] = foundry.TimeoutSeconds.ToString()
                    });
                try
                {
                    _logger.LogWarning(
                        ex,
                        "Foundry extraction failed due to timeout or linked cancellation. Attempt {Attempt}/{MaxAttempts}.",
                        attempt + 1,
                        maxAttempts);
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                TryWriteFoundryErrorFile(
                    LlmFoundryFileLogSeverity.Error,
                    "FoundryRequestTransportError",
                    "Failed to complete HTTP request to Azure AI Foundry for address extraction.",
                    ex,
                    new Dictionary<string, string?>
                    {
                        ["documentType"] = documentType,
                        ["attempt"] = (attempt + 1).ToString(),
                        ["maxAttempts"] = maxAttempts.ToString(),
                        ["requestPath"] = SafeLogUriPath(requestUri)
                    });
                try
                {
                    _logger.LogError(
                        ex,
                        "Foundry extraction failed with a transport/HTTP error. Attempt {Attempt}/{MaxAttempts}.",
                        attempt + 1,
                        maxAttempts);
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private void TryWriteFoundryErrorFile(
        LlmFoundryFileLogSeverity severity,
        string eventType,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string?>? context = null)
    {
        try
        {
            _errorFileLog.Write(severity, eventType, message, exception, context);
        }
        catch
        {
            // Secondary observability only; must not affect extraction.
        }
    }

    private static string BuildRequestUri(AddressExtractionFoundryOptions options)
    {
        var baseUri = options.Endpoint.TrimEnd('/');
        if (baseUri.Contains("api-version", StringComparison.OrdinalIgnoreCase))
        {
            return baseUri;
        }
        if (baseUri.Contains("openai/responses", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUri}?api-version={Uri.EscapeDataString(options.ApiVersion)}";
        }
        return $"{baseUri}/chat/completions?api-version={Uri.EscapeDataString(options.ApiVersion)}";
    }

    private string BuildRequestBody(string ocrText, string documentType, int attempt, bool useResponsesApi)
    {
        var foundry = _options.Foundry;
        var prompt = attempt == 0
            ? AddressExtractionSystemPrompt
            : AddressExtractionRetryPrompt;

        if (useResponsesApi)
        {
            var responsesRequest = new
            {
                model = foundry.Model,
                text = new { format = new { type = "json_object" } },
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new object[]
                        {
                            new { type = "input_text", text = prompt }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "input_text", text = $"DocumentType: {documentType}\n\nOCRText:\n{ocrText}" }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(responsesRequest, JsonOptions);
        }

        var chatCompletionsRequest = new
        {
            model = foundry.Model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = prompt },
                new
                {
                    role = "user",
                    content = $"DocumentType: {documentType}\n\nOCRText:\n{ocrText}"
                }
            }
        };

        return JsonSerializer.Serialize(chatCompletionsRequest, JsonOptions);
    }

    private static AddressExtractionLlmResult? TryParseResult(
        string payload,
        bool useResponsesApi,
        out string? parseError,
        out Exception? parseException)
    {
        parseError = null;
        parseException = null;
        try
        {
            using var root = JsonDocument.Parse(payload);
            var content = useResponsesApi
                ? ExtractResponsesContent(root.RootElement)
                : ExtractChatCompletionsContent(root.RootElement);
            if (string.IsNullOrWhiteSpace(content))
            {
                parseError = "No content returned by model response.";
                return null;
            }

            var jsonPayload = ExtractJsonObject(content);
            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                parseError = "Model content did not include a JSON object.";
                return null;
            }

            using var contentDoc = JsonDocument.Parse(jsonPayload);
            var contentRoot = contentDoc.RootElement;
            var tenants = new List<string>();
            if (contentRoot.TryGetProperty("tenants", out var tenantsElement)
                && tenantsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tenantsElement.EnumerateArray())
                {
                    var name = item.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        tenants.Add(name.Trim());
                    }
                }
            }

            var address = contentRoot.TryGetProperty("address", out var addressElement)
                ? addressElement.GetString() ?? string.Empty
                : string.Empty;
            var confidence = ParseConfidence(contentRoot);

            return new AddressExtractionLlmResult
            {
                Tenants = tenants.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Address = address.Trim(),
                Confidence = confidence
            };
        }
        catch (Exception ex)
        {
            parseError = ex.Message;
            parseException = ex;
            return null;
        }
    }

    private static string? SafeLogUriPath(string? requestUri)
    {
        if (string.IsNullOrWhiteSpace(requestUri) || !Uri.TryCreate(requestUri, UriKind.Absolute, out var uri))
        {
            return requestUri;
        }

        return uri.GetLeftPart(UriPartial.Path);
    }

    private static bool IsResponsesApiEndpoint(string requestUri)
    {
        return requestUri.Contains("openai/responses", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractChatCompletionsContent(JsonElement root)
    {
        return root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private static string? ExtractResponsesContent(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

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

    private void WriteRawResponseArtifact(string payload, string documentType, int attempt)
    {
        try
        {
            var artifactDirectory = ResolveArtifactDirectory();
            var record = new
            {
                generatedAtUtc = DateTime.UtcNow,
                documentType,
                attempt,
                rawResponse = payload
            };

            var historyFilePath = Path.Combine(artifactDirectory, "llm_dev_raw_response_history.jsonl");
            AppendJsonLine(historyFilePath, record);
        }
        catch (Exception ex)
        {
            try
            {
                _logger.LogWarning(ex, "Could not write dev raw LLM response artifact.");
            }
            catch
            {
            }
        }
    }

    private void WriteParsedResultArtifact(AddressExtractionLlmResult parsed, string documentType)
    {
        try
        {
            var artifactDirectory = ResolveArtifactDirectory();
            var record = new
            {
                generatedAtUtc = DateTime.UtcNow,
                documentType,
                tenants = parsed.Tenants,
                address = parsed.Address,
                confidence = parsed.Confidence
            };

            var historyFilePath = Path.Combine(artifactDirectory, "llm_dev_parsed_result_history.jsonl");
            AppendJsonLine(historyFilePath, record);
        }
        catch (Exception ex)
        {
            try
            {
                _logger.LogWarning(ex, "Could not write dev parsed LLM artifact.");
            }
            catch
            {
            }
        }
    }

    private static string ResolveArtifactDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var projectDir = Path.Combine(dir.FullName, "src", "LeaseLense.Web");
            if (File.Exists(Path.Combine(projectDir, "LeaseLense.Web.csproj")))
            {
                var artifactsPath = Path.Combine(projectDir, "artifacts");
                Directory.CreateDirectory(artifactsPath);
                return artifactsPath;
            }

            dir = dir.Parent;
        }

        var fallbackPath = Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(fallbackPath);
        return fallbackPath;
    }

    private static void AppendJsonLine(string path, object value)
    {
        var line = JsonSerializer.Serialize(value);
        File.AppendAllText(path, line + Environment.NewLine);
    }
}
