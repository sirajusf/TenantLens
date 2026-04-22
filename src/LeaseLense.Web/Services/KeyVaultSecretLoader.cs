using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LeaseLense.Web.Services;

public sealed class KeyVaultSecretMapping
{
    public string ConfigurationKey { get; set; } = string.Empty;
    public string SecretName { get; set; } = string.Empty;
}

public static class KeyVaultSecretLoader
{
    public static async Task ApplyAsync(IConfiguration configuration, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var vaultUri = configuration["KeyVault:VaultUri"]?.Trim();
        if (string.IsNullOrEmpty(vaultUri))
        {
            return;
        }

        if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) || uri.Scheme != "https")
        {
            logger?.LogWarning("Key Vault URI must be an absolute https URL. Skipping secret resolution.");
            return;
        }

        var mappings = new List<KeyVaultSecretMapping>();
        configuration.GetSection("KeyVaultSecretMappings").Bind(mappings);
        if (mappings.Count == 0)
        {
            logger?.LogWarning("KeyVault:VaultUri is set but KeyVaultSecretMappings is empty.");
            return;
        }

        var client = new SecretClient(uri, new DefaultAzureCredential());

        foreach (var map in mappings)
        {
            var configurationKey = map.ConfigurationKey?.Trim();
            var secretName = map.SecretName?.Trim();
            if (string.IsNullOrEmpty(configurationKey) || string.IsNullOrEmpty(secretName))
            {
                continue;
            }

            try
            {
                var response = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);
                var value = response.Value.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    configuration[configurationKey] = value;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load Key Vault secret for configuration key {ConfigKey} (secret name: {SecretName}).", configurationKey, secretName);
                throw;
            }
        }
    }
}
