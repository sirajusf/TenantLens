# Running Tests

This folder contains test projects for LeaseLense.

## Product documentation

- [Address verification flow](../docs/address-verification.md)
- [Configuration and Key Vault](../docs/configuration.md)

## Prerequisites

- .NET SDK installed (project currently targets `net10.0`)
- Run commands from repository root:
  - `C:/Users/cmdsi/OneDrive/Desktop/My Files/University of South Florida/Semester 3 (Spring 2026)/ADA/Project/LeaseLense`

## Run all web tests

```powershell
dotnet test tests/LeaseLense.Web.Tests/LeaseLense.Web.Tests.csproj
```

## Run a single test class

```powershell
dotnet test tests/LeaseLense.Web.Tests/LeaseLense.Web.Tests.csproj --filter "FullyQualifiedName~AzureFoundryAddressExtractionLlmClientTests"
```

## Run LLM fallback isolation test

This test can run in two modes:

1. **Sample OCR mode** (no document URL)
2. **Document URL mode** (downloads file from URL, runs DI layout OCR, then calls LLM fallback)

### Required toggle (enables live isolation run)

```powershell
$env:RUN_LLM_FALLBACK_ISOLATION="true"
```

### Optional: pass a document URL

```powershell
$env:LLM_FALLBACK_DOC_URL="https://example.com/lease.pdf"
```

### Execute isolation test

```powershell
dotnet test tests/LeaseLense.Web.Tests/LeaseLense.Web.Tests.csproj --filter "FullyQualifiedName~LlmFallbackIsolationTests"
```

### Show full test output in terminal (recommended)

```powershell
dotnet test tests/LeaseLense.Web.Tests/LeaseLense.Web.Tests.csproj --filter "FullyQualifiedName~LlmFallbackIsolationTests" --logger "console;verbosity=detailed"
```

### Expected output (in terminal/test output)

- Foundry endpoint/model/version
- HTTP request/response body (truncated)
- Raw LLM response body
- Parsed tenants/address result
- (If URL provided) OCR preview and OCR text length

## Reset environment variables (optional)

```powershell
Remove-Item Env:RUN_LLM_FALLBACK_ISOLATION -ErrorAction SilentlyContinue
Remove-Item Env:LLM_FALLBACK_DOC_URL -ErrorAction SilentlyContinue
```
