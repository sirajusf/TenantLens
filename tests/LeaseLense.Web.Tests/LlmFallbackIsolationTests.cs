using System.Net.Http;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.DocumentIntelligence;
using LeaseLense.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace LeaseLense.Web.Tests;

public sealed class LlmFallbackIsolationTests
{
    private readonly ITestOutputHelper _output;

    public LlmFallbackIsolationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RealFoundryFallback_IsolationRun_LogsEverything()
    {
        var runLive = string.Equals(
            Environment.GetEnvironmentVariable("RUN_LLM_FALLBACK_ISOLATION"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (!runLive)
        {
            _output.WriteLine("Skipping live LLM isolation run. Set RUN_LLM_FALLBACK_ISOLATION=true to execute.");
            return;
        }

        var devSettingsPath = ResolveDevelopmentSettingsPath();
        _output.WriteLine($"Using settings file: {devSettingsPath}");
        var options = LoadFoundryOptions(devSettingsPath);
        if (!options.Foundry.Enabled)
        {
            throw new InvalidOperationException("Foundry is disabled in appsettings.Development.json.");
        }

        _output.WriteLine($"Foundry endpoint: {options.Foundry.Endpoint}");
        _output.WriteLine($"Foundry model: {options.Foundry.Model}");
        _output.WriteLine($"Foundry apiVersion: {options.Foundry.ApiVersion}");
        _output.WriteLine($"Foundry maxRetries: {options.Foundry.MaxRetries}");

        var docUrl = Environment.GetEnvironmentVariable("LLM_FALLBACK_DOC_URL");
        _output.WriteLine($"LLM_FALLBACK_DOC_URL: {(string.IsNullOrWhiteSpace(docUrl) ? "<not set>" : docUrl)}");

        string ocrText;
        if (!string.IsNullOrWhiteSpace(docUrl))
        {
            var documentBytes = await LoadDocumentBytesAsync(docUrl, _output);
            _output.WriteLine($"Loaded bytes: {documentBytes.Length}");

            _output.WriteLine("Running Document Intelligence layout OCR...");
            var diClient = new DocumentIntelligenceClient(
                new Uri(options.Endpoint),
                new AzureKeyCredential(options.ApiKey));
            var operation = await diClient.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                options.LayoutFallbackModelId,
                BinaryData.FromBytes(documentBytes));
            ocrText = operation.Value.Content ?? string.Empty;
            _output.WriteLine($"OCR text length: {ocrText.Length}");
            _output.WriteLine("=== OCR Preview ===");
            _output.WriteLine(Truncate(ocrText, 4000));
        }
        else
        {
            ocrText = """
1. PARTIES. THIS LEASE CONTRACT IS BETWEEN YOU, THE RESIDENT(S): JOHN DOE, JANE DOE
2. PREMISES. 14216 CYBER PL APT 301, TAMPA, FL 33613
3. TERM. START DATE 2026-01-01 END DATE 2026-12-31
""";
            _output.WriteLine("Using built-in sample OCR text. Set LLM_FALLBACK_DOC_URL to test your own lease document URL.");
        }

        var loggingHandler = new LoggingHandler(_output)
        {
            InnerHandler = new HttpClientHandler()
        };
        var httpClient = new HttpClient(loggingHandler);
        var client = new AzureFoundryAddressExtractionLlmClient(
            httpClient,
            Options.Create(options),
            Options.Create(new LlmFoundryFileLoggingOptions { Enabled = false }),
            NullLogger<AzureFoundryAddressExtractionLlmClient>.Instance,
            NullLlmFoundryErrorFileLog.Instance);

        _output.WriteLine("Calling TryExtractAsync(documentType=lease)...");
        var result = await client.TryExtractAsync(ocrText, "lease");
        _output.WriteLine("=== Raw LLM Output (latest response body) ===");
        _output.WriteLine(loggingHandler.LastResponseBody ?? "<no response body>");
        var artifactDirectory = ResolveArtifactDirectory();
        var rawResponsePath = Path.Combine(artifactDirectory, "llm_fallback_last_raw_response.json");
        await File.WriteAllTextAsync(rawResponsePath, loggingHandler.LastResponseBody ?? "<no response body>");
        _output.WriteLine($"Saved raw LLM response to: {rawResponsePath}");

        if (result is null)
        {
            _output.WriteLine("Parsed result: <null>");
            throw new Xunit.Sdk.XunitException("LLM fallback returned null (check logs above for HTTP/response details).");
        }

        _output.WriteLine("=== Parsed LLM Output ===");
        _output.WriteLine($"Parsed tenants count: {result.Tenants.Count}");
        foreach (var tenant in result.Tenants)
        {
            _output.WriteLine($"Tenant: {tenant}");
        }

        _output.WriteLine($"Parsed address: {result.Address}");
        var parsedResultPath = Path.Combine(artifactDirectory, "llm_fallback_last_parsed_result.json");
        var parsedResultJson = JsonSerializer.Serialize(new
        {
            generatedAtUtc = DateTime.UtcNow,
            documentSource = docUrl ?? "<built-in-sample>",
            tenants = result.Tenants,
            address = result.Address
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(parsedResultPath, parsedResultJson);
        _output.WriteLine($"Saved parsed LLM output to: {parsedResultPath}");
        Assert.True(result.Tenants.Count >= 0);
    }

    private static string ResolveDevelopmentSettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "LeaseLense.Web", "appsettings.Development.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find src/LeaseLense.Web/appsettings.Development.json from test runtime path.");
    }

    private static string ResolveArtifactDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "LeaseLense.Web.Tests", "artifacts");
            if (Directory.Exists(Path.Combine(dir.FullName, "tests", "LeaseLense.Web.Tests")))
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            dir = dir.Parent;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static AzureDocumentIntelligenceOptions LoadFoundryOptions(string settingsPath)
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath));
        if (!json.RootElement.TryGetProperty("AzureDocumentIntelligence", out var di))
        {
            throw new InvalidOperationException("Missing AzureDocumentIntelligence section.");
        }

        var foundry = di.GetProperty("Foundry");
        return new AzureDocumentIntelligenceOptions
        {
            Enabled = di.GetProperty("Enabled").GetBoolean(),
            Endpoint = di.GetProperty("Endpoint").GetString() ?? string.Empty,
            ApiKey = di.GetProperty("ApiKey").GetString() ?? string.Empty,
            LayoutFallbackModelId = di.TryGetProperty("LayoutFallbackModelId", out var layoutModel)
                ? layoutModel.GetString() ?? "prebuilt-layout"
                : "prebuilt-layout",
            Foundry = new AddressExtractionFoundryOptions
            {
                Enabled = foundry.GetProperty("Enabled").GetBoolean(),
                Endpoint = foundry.GetProperty("Endpoint").GetString() ?? string.Empty,
                ApiKey = foundry.GetProperty("ApiKey").GetString() ?? string.Empty,
                Model = foundry.GetProperty("Model").GetString() ?? string.Empty,
                ApiVersion = foundry.GetProperty("ApiVersion").GetString() ?? "2024-05-01-preview",
                TimeoutSeconds = foundry.GetProperty("TimeoutSeconds").GetInt32(),
                MaxRetries = foundry.GetProperty("MaxRetries").GetInt32()
            }
        };
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max] + "...<truncated>";
    }

    private static async Task<byte[]> LoadDocumentBytesAsync(string input, ITestOutputHelper output)
    {
        if (Uri.TryCreate(input, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile)
            {
                var filePath = absoluteUri.LocalPath;
                output.WriteLine($"Loading lease document from file URI: {filePath}");
                return await File.ReadAllBytesAsync(filePath);
            }

            if (absoluteUri.Scheme is "http" or "https")
            {
                output.WriteLine($"Downloading lease document from URL: {absoluteUri}");
                using var downloadClient = new HttpClient();
                return await downloadClient.GetByteArrayAsync(absoluteUri);
            }
        }

        output.WriteLine($"Loading lease document from local path: {input}");
        return await File.ReadAllBytesAsync(input);
    }

    private sealed class LoggingHandler : DelegatingHandler
    {
        private readonly ITestOutputHelper _output;
        public string? LastResponseBody { get; private set; }

        public LoggingHandler(ITestOutputHelper output)
        {
            _output = output;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var reqBody = request.Content is null ? "<no body>" : await request.Content.ReadAsStringAsync(cancellationToken);
            _output.WriteLine($"HTTP Request: {request.Method} {request.RequestUri}");
            _output.WriteLine($"HTTP Request Body: {Truncate(reqBody, 20000)}");

            var response = await base.SendAsync(request, cancellationToken);
            var resBody = response.Content is null ? "<no body>" : await response.Content.ReadAsStringAsync(cancellationToken);
            LastResponseBody = resBody;
            _output.WriteLine($"HTTP Response: {(int)response.StatusCode} {response.StatusCode}");
            _output.WriteLine($"HTTP Response Body: {Truncate(resBody, 20000)}");

            if (response.Content is not null)
            {
                response.Content = new StringContent(resBody, Encoding.UTF8, response.Content.Headers.ContentType?.MediaType ?? "application/json");
            }

            return response;
        }

        private static string Truncate(string value, int max)
        {
            return value.Length <= max ? value : value[..max] + "...<truncated>";
        }
    }
}
