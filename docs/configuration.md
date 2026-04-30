# Configuration and secrets

## Where settings should live

| Kind of value | Local dev | Production (recommended) |
|---------------|-----------|---------------------------|
| Passwords, API keys, full connection strings | User Secrets, `appsettings.Development.json` (not committed) | **Azure Key Vault** (see below), loaded via [`KeyVaultSecretLoader`](../src/LeaseLense.Web/Services/KeyVaultSecretLoader.cs) |
| Host base URL, feature `Enabled`, model IDs, timeouts, ports | `appsettings.json` / `appsettings.{Environment}.json` | Same, or **Azure App Service / Container app settings** (non-secret) |
| Environment-specific tuning without redeploy | User Secrets, local JSON | **Azure App Configuration** (optional), or slot settings |

**Rule of thumb:** if it would hurt if it leaked, it belongs in **Key Vault** (or another secret store), not in Git or plain app settings.

---

## Local development

- Use **User Secrets** (`dotnet user-secrets`) or a local **`appsettings.Development.json`** (not committed) for connection strings, Gmail app password, and Azure keys.
- Leave **`KeyVault:VaultUri`** empty to skip Key Vault during startup.
- To test against a real vault from your machine, set `KeyVault:VaultUri` and sign in with **Azure CLI** (`az login`) or use Visual Studio / **DefaultAzureCredential** as documented on the [Azure.Identity](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential) page.

---

## Production (Azure)

### 1. Secrets — Key Vault

1. Create a Key Vault per environment (or one vault with secret name prefixes per env).
2. Create secrets whose **names** match the `SecretName` values in [`KeyVaultSecretMappings`](../src/LeaseLense.Web/appsettings.json) (or adjust the JSON to match your naming). Store the **full** connection string and raw key values as secret **values**.
3. Set **`KeyVault:VaultUri`** in the hosting environment (e.g. App Service **Configuration** application setting) to `https://<vault-name>.vault.azure.net/`. Do not commit the production URI to public repos if it is sensitive; use environment variables or secure pipeline variables.
4. Enable **Managed Identity** on the App Service (or workload identity on AKS). Grant that identity **Key Vault Secrets User** (RBAC: `get` on secrets) on the vault. **Do not** put Key Vault client secrets in app settings for production if you can use managed identity.
5. On startup, [`KeyVaultSecretLoader`](../src/LeaseLense.Web/Services/KeyVaultSecretLoader.cs) runs **before** DI registration and injects resolved values into `IConfiguration` for the keys listed in **`KeyVaultSecretMappings`**.

**Authentication:** the app uses **`DefaultAzureCredential`**: in Azure it prefers **managed identity**; locally it can use Azure CLI, Visual Studio, or environment-based credentials.

**Current template mappings (secrets only):**

| Configuration key | Purpose |
|-------------------|--------|
| `ConnectionStrings:DefaultConnection` | SQL (production default) |
| `ConnectionStrings:LocalSqlConnection` | SQL (optional; e.g. staging) |
| `Email:GmailSmtp:AppPassword` | Gmail SMTP app password |
| `AzureDocumentIntelligence:ApiKey` | Document Intelligence |
| `AzureDocumentIntelligence:Foundry:ApiKey` | Azure AI / Foundry LLM |

Non-secrets (`Endpoint`, `ModelId`, `Enabled`, `TimeoutSeconds`, `LlmFoundryFileLogging`, etc.) stay in **appsettings** or **host configuration** so you can change behavior without storing them as Key Vault secrets.

**Security:** secret values are never written to logs. By default, if a secret cannot be loaded, the app logs the issue and continues with existing configuration values (for example from `appsettings` or environment variables). Set `KeyVault:FailFast` to `true` when you want startup to fail immediately on Key Vault errors.

**Rotation:** rotate the secret in Key Vault, then **restart** the app so the loader runs again and picks up the new value.

### 2. Non-secrets (production)

- Keep **defaults** in committed [`appsettings.json`](../src/LeaseLense.Web/appsettings.json) for model IDs, thresholds, and structure.
- Override **per environment** with `appsettings.Production.json` deployed with the app, or with **Application settings** in the Azure portal (flattened keys with `:` or `__` for nested settings).
- For frequent toggles or shared config across many services, consider **Azure App Configuration**; this app does not require it for a basic production deployment.

### 3. Template `appsettings.json`

The committed file uses **empty** placeholders for sensitive fields and lists **`KeyVaultSecretMappings`** for documentation. **Do not** commit real passwords, API keys, or production connection strings. Production URIs and overrides should come from the host or pipeline.

Example Key Vault block (for production; secret names can match your vault):

```json
"KeyVault": {
  "VaultUri": "https://your-vault.vault.azure.net/"
},
"KeyVaultSecretMappings": [
  { "ConfigurationKey": "ConnectionStrings:DefaultConnection", "SecretName": "LeaseLense-Sql-DefaultConnection" },
  { "ConfigurationKey": "ConnectionStrings:LocalSqlConnection", "SecretName": "LeaseLense-Sql-LocalConnection" },
  { "ConfigurationKey": "Email:GmailSmtp:AppPassword", "SecretName": "LeaseLense-Gmail-AppPassword" },
  { "ConfigurationKey": "AzureDocumentIntelligence:ApiKey", "SecretName": "LeaseLense-DocumentIntelligence-ApiKey" },
  { "ConfigurationKey": "AzureDocumentIntelligence:Foundry:ApiKey", "SecretName": "LeaseLense-Foundry-ApiKey" }
]
```

Add more `{ "ConfigurationKey", "SecretName" }` pairs only if you need additional secrets; keep the **configuration key** path aligned with `IOptions<>` sections and `GetConnectionString` (see the main [README](../README.md)).
