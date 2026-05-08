using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class KeycloakOptionsValidator : IValidateOptions<KeycloakOptions>
{
    private static readonly string[] s_disallowedProductionHosts =
    {
        "localhost",
        "127.0.0.1",
        "keycloak"
    };

    private readonly IHostEnvironment _environment;

    public KeycloakOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, KeycloakOptions options)
    {
        List<string> failures = new();

        Uri? baseUri = null;

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add($"{KeycloakOptions.SectionName}:BaseUrl is required.");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out baseUri)
                 || string.IsNullOrWhiteSpace(baseUri.Host))
        {
            failures.Add($"{KeycloakOptions.SectionName}:BaseUrl must be a valid absolute URI.");
            baseUri = null;
        }
        else if (baseUri.AbsolutePath.Contains("/realms/", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{KeycloakOptions.SectionName}:BaseUrl must be the Keycloak base URL only and must not contain '/realms/...'.");
        }

        if (string.IsNullOrWhiteSpace(options.OwnerRealm))
        {
            failures.Add($"{KeycloakOptions.SectionName}:OwnerRealm is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{KeycloakOptions.SectionName}:Audience is required.");
        }

        if (_environment.IsProduction())
        {
            if (baseUri is not null
                && !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{KeycloakOptions.SectionName}:BaseUrl must use https in Production.");
            }

            if (!options.RequireHttpsMetadata)
            {
                failures.Add(
                    $"{KeycloakOptions.SectionName}:RequireHttpsMetadata must be true in Production.");
            }

            if (baseUri is not null
                && s_disallowedProductionHosts.Contains(baseUri.Host, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{KeycloakOptions.SectionName}:BaseUrl host '{baseUri.Host}' is not allowed in Production. " +
                    "Use the public Keycloak hostname.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
