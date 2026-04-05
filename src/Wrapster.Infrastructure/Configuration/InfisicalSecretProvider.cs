using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wrapster.Infrastructure.Configuration;

public static class InfisicalSecretProvider
{
    private static readonly Dictionary<string, string> SecretConfigMappings = new()
    {
        ["RESEND_API_KEY"] = "Resend:ApiKey"
    };

    public static IServiceCollection AddInfisicalSecrets(
        this IServiceCollection services,
        IConfigurationManager configuration,
        ILogger logger,
        string environmentName)
    {
        IConfigurationSection infisicalSection = configuration.GetSection(InfisicalOptions.SectionName);
        string? clientId = infisicalSection[nameof(InfisicalOptions.ClientId)];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            if (infisicalSection.Exists() && infisicalSection.GetChildren().Any())
            {
                throw new InvalidOperationException(
                    "Infisical section is configured but ClientId is missing. " +
                    "Either provide a valid ClientId or remove the Infisical section entirely.");
            }

            logger.LogInformation("Infisical is not configured. Skipping secret fetch");
            return services;
        }

        string clientSecret = infisicalSection[nameof(InfisicalOptions.ClientSecret)]
                              ?? throw new InvalidOperationException(
                                  "Infisical:ClientSecret is required when Infisical:ClientId is configured.");

        string projectId = infisicalSection[nameof(InfisicalOptions.ProjectId)]
                           ?? throw new InvalidOperationException(
                               "Infisical:ProjectId is required when Infisical:ClientId is configured.");

        string hostUri = infisicalSection[nameof(InfisicalOptions.HostUri)]
                         ?? "https://app.infisical.com";

        string environment = infisicalSection[nameof(InfisicalOptions.Environment)]
                             ?? MapEnvironmentSlug(environmentName);

        string secretPath = infisicalSection[nameof(InfisicalOptions.SecretPath)] ?? "/";

        logger.LogInformation(
            "Fetching secrets from Infisical (project: {ProjectId}, env: {Environment}, path: {SecretPath})",
            projectId, environment, secretPath);

        try
        {
            FetchAndInjectSecrets(
                configuration, logger, clientId, clientSecret,
                projectId, hostUri, environment, secretPath);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to fetch secrets from Infisical. Application cannot start securely");
            throw new InvalidOperationException(
                "Failed to fetch secrets from Infisical. See inner exception for details.", ex);
        }

        return services;
    }

    // Separate method so the internal SDK types resolved via 'var' stay contained here.
    // The project enforces explicit types (no 'var'), but the Infisical SDK marks its
    // response types as internal, making them unreferenceable by name. Using 'var' is
    // the only option for these SDK return values.
#pragma warning disable IDE0008
    private static void FetchAndInjectSecrets(
        IConfigurationManager configuration,
        ILogger logger,
        string clientId,
        string clientSecret,
        string projectId,
        string hostUri,
        string environment,
        string secretPath)
    {
        InfisicalSdkSettings settings = new InfisicalSdkSettingsBuilder()
            .WithHostUri(hostUri)
            .Build();

        InfisicalClient infisicalClient = new(settings);

        infisicalClient.Auth()
            .UniversalAuth()
            .LoginAsync(clientId, clientSecret)
            .GetAwaiter()
            .GetResult();

        ListSecretsOptions listOptions = new()
        {
            EnvironmentSlug = environment,
            SecretPath = secretPath,
            ProjectId = projectId,
            ExpandSecretReferences = true,
            ViewSecretValue = true
        };

        Secret[]? secrets = infisicalClient.Secrets()
            .ListAsync(listOptions)
            .GetAwaiter()
            .GetResult();

        if (secrets is null || secrets.Length == 0)
        {
            logger.LogWarning("No secrets returned from Infisical for environment '{Environment}'", environment);
            return;
        }

        Dictionary<string, string?> configOverrides = new();

        foreach (Secret secret in secrets)
        {
            if (string.IsNullOrWhiteSpace(secret.SecretValue))
            {
                continue;
            }

            if (SecretConfigMappings.TryGetValue(secret.SecretKey, out string? configPath)
                && configPath is not null)
            {
                configOverrides[configPath] = secret.SecretValue;
                logger.LogDebug("Mapped Infisical secret '{SecretName}' -> {ConfigPath}",
                    secret.SecretKey, configPath);
            }
        }

        if (configOverrides.Count > 0)
        {
            configuration.AddInMemoryCollection(configOverrides);
            logger.LogInformation("Injected {Count} configuration values from Infisical",
                configOverrides.Count);
        }
    }
#pragma warning restore IDE0008

    private static string MapEnvironmentSlug(string aspNetEnvironment) => aspNetEnvironment.ToLowerInvariant() switch
    {
        "development" => "dev",
        "staging" => "staging",
        "production" => "prod",
        _ => "dev"
    };
}
