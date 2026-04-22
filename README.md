# LeaseLense

Renter-focused web app for property reviews, scam reports, and **verified stay** badges backed by document-based residency checks.

## Documentation

- [Address and residency verification](docs/address-verification.md) — flow, matching rules, thresholds, ops notes.
- [Configuration and secrets](docs/configuration.md) — **local vs production**, Key Vault, Managed Identity, what belongs in the vault vs `appsettings`.

## Development

- Target framework: **.NET 10** (`net10.0`).
- Web project: `src/LeaseLense.Web`.
- **Local:** use **User Secrets** or `appsettings.Development.json` (not committed) for secrets. Leave `KeyVault:VaultUri` empty unless you are testing against a real vault.
- **Production:** store secrets in **Azure Key Vault**, use **Managed Identity** and **`KeyVaultSecretMappings`**; keep non-secrets (endpoints, model IDs, feature flags) in `appsettings` or app settings. See [docs/configuration.md](docs/configuration.md).

## Tests

See [tests/README.md](tests/README.md).
