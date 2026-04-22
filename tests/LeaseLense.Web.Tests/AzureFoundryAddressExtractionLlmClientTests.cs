using System.Net;
using System.Net.Http;
using System.Text;
using LeaseLense.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LeaseLense.Web.Tests;

public sealed class AzureFoundryAddressExtractionLlmClientTests
{
    [Fact]
    public async Task TryExtractAsync_ReturnsTenantsAndAddress_FromJsonContent()
    {
        var payload = """
{
  "choices": [
    {
      "message": {
        "content": "{ \"tenants\": [\"Alex Doe\", \"Jamie Doe\"], \"address\": \"123 Main St, Tampa, FL\", \"confidence\": 0.82 }"
      }
    }
  ]
}
""";

        var client = BuildClient(payload);
        var result = await client.TryExtractAsync("mock ocr", "lease");

        Assert.NotNull(result);
        Assert.Equal(2, result.Tenants.Count);
        Assert.Contains("Alex Doe", result.Tenants);
        Assert.Equal("123 Main St, Tampa, FL", result.Address);
        Assert.Equal(0.82m, result.Confidence);
    }

    [Fact]
    public async Task TryExtractAsync_ReturnsNull_ForMalformedJsonContent()
    {
        var payload = """
{
  "choices": [
    {
      "message": {
        "content": "{ not-json }"
      }
    }
  ]
}
""";

        var client = BuildClient(payload);
        var result = await client.TryExtractAsync("mock ocr", "utility_bill");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryExtractAsync_ReturnsTenantsAndAddress_FromResponsesApiContent()
    {
        var payload = """
{
  "output": [
    {
      "content": [
        {
          "type": "output_text",
          "text": "{\"tenants\":[\"Alex Doe\"],\"address\":\"123 Main St, Tampa, FL\",\"confidence\":0.91}"
        }
      ]
    }
  ]
}
""";

        var client = BuildClient(payload, "https://example.services.ai.azure.com/openai/responses");
        var result = await client.TryExtractAsync("mock ocr", "lease");

        Assert.NotNull(result);
        Assert.Single(result.Tenants);
        Assert.Equal("Alex Doe", result.Tenants[0]);
        Assert.Equal("123 Main St, Tampa, FL", result.Address);
        Assert.Equal(0.91m, result.Confidence);
    }

    [Fact]
    public async Task TryExtractAsync_ParsesJsonWrappedInMarkdownFence()
    {
        var payload = """
{
  "choices": [
    {
      "message": {
        "content": "```json\n{\"tenants\":[\"Mahammad Siraj Cherun\"],\"address\":\"4202 E Fowler Ave, Tampa, FL\",\"confidence\":0.73}\n```"
      }
    }
  ]
}
""";

        var client = BuildClient(payload);
        var result = await client.TryExtractAsync("mock ocr", "lease");

        Assert.NotNull(result);
        Assert.Single(result.Tenants);
        Assert.Equal("Mahammad Siraj Cherun", result.Tenants[0]);
        Assert.Equal("4202 E Fowler Ave, Tampa, FL", result.Address);
        Assert.Equal(0.73m, result.Confidence);
    }

    [Fact]
    public async Task TryExtractAsync_ParsesJsonWithLeadingAndTrailingProse()
    {
        var payload = """
{
  "choices": [
    {
      "message": {
        "content": "Here is the extracted result:\n{\"tenants\":[\"Alex Doe\"],\"address\":\"123 Main St, Tampa, FL\",\"confidence\":0.66}\nThanks."
      }
    }
  ]
}
""";

        var client = BuildClient(payload);
        var result = await client.TryExtractAsync("mock ocr", "lease");

        Assert.NotNull(result);
        Assert.Single(result.Tenants);
        Assert.Equal("Alex Doe", result.Tenants[0]);
        Assert.Equal("123 Main St, Tampa, FL", result.Address);
        Assert.Equal(0.66m, result.Confidence);
    }

    [Fact]
    public async Task TryExtractAsync_UsesDefaultConfidence_WhenMissing()
    {
        var payload = """
{
  "choices": [
    {
      "message": {
        "content": "{\"tenants\":[\"Alex Doe\"],\"address\":\"123 Main St, Tampa, FL\"}"
      }
    }
  ]
}
""";

        var client = BuildClient(payload);
        var result = await client.TryExtractAsync("mock ocr", "lease");

        Assert.NotNull(result);
        Assert.Equal(0.5m, result.Confidence);
    }

    [Fact]
    public async Task TryExtractAsync_ClampsConfidence_WhenOutOfRange()
    {
        var payload = """
{
  "choices": [
    {
      "message": {
        "content": "{\"tenants\":[\"Alex Doe\"],\"address\":\"123 Main St, Tampa, FL\",\"confidence\":1.4}"
      }
    }
  ]
}
""";

        var client = BuildClient(payload);
        var result = await client.TryExtractAsync("mock ocr", "lease");

        Assert.NotNull(result);
        Assert.Equal(1m, result.Confidence);
    }

    [Fact]
    public async Task TryExtractAsync_ReturnsNull_WhenHttpThrows_DoesNotPropagate()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Simulated network failure"));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AzureDocumentIntelligenceOptions
        {
            Foundry = new AddressExtractionFoundryOptions
            {
                Enabled = true,
                Endpoint = "https://example.openai.azure.com/openai/deployments/x/chat/completions",
                ApiKey = "test-key",
                Model = "m",
                ApiVersion = "2024-05-01-preview",
                MaxRetries = 0,
                TimeoutSeconds = 5
            }
        });
        var client = new AzureFoundryAddressExtractionLlmClient(
            httpClient,
            options,
            Options.Create(new LlmFoundryFileLoggingOptions { Enabled = false }),
            NullLogger<AzureFoundryAddressExtractionLlmClient>.Instance,
            NullLlmFoundryErrorFileLog.Instance);

        var result = await client.TryExtractAsync("ocr", "lease");

        Assert.Null(result);
    }

    private static AzureFoundryAddressExtractionLlmClient BuildClient(
        string payload,
        string endpoint = "https://example.openai.azure.com/openai/deployments/test-model")
    {
        var handler = new StubHttpMessageHandler(payload);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AzureDocumentIntelligenceOptions
        {
            Foundry = new AddressExtractionFoundryOptions
            {
                Enabled = true,
                Endpoint = endpoint,
                ApiKey = "test-key",
                Model = "test-model",
                ApiVersion = "2024-05-01-preview",
                TimeoutSeconds = 5
            }
        });

        return new AzureFoundryAddressExtractionLlmClient(
            httpClient,
            options,
            Options.Create(new LlmFoundryFileLoggingOptions { Enabled = false }),
            NullLogger<AzureFoundryAddressExtractionLlmClient>.Instance,
            NullLlmFoundryErrorFileLog.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;

        public StubHttpMessageHandler(string response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(_exception);
        }
    }
}
