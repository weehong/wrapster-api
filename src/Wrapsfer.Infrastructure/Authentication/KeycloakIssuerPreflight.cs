using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class KeycloakIssuerPreflight : IHostedService
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<KeycloakIssuerPreflight> _logger;
    private readonly KeycloakOptions _options;
    private readonly RealmConfigurationCache _realmConfigurationCache;

    public KeycloakIssuerPreflight(
        IOptions<KeycloakOptions> options,
        RealmConfigurationCache realmConfigurationCache,
        IHostEnvironment environment,
        ILogger<KeycloakIssuerPreflight> logger)
    {
        _options = options.Value;
        _realmConfigurationCache = realmConfigurationCache;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string expectedIssuer = $"{_options.BaseUrl.TrimEnd('/')}/realms/{_options.OwnerRealm}";

        OpenIdConnectConfiguration configuration;

        try
        {
            configuration = await _realmConfigurationCache
                .GetConfigurationAsync(_options.OwnerRealm, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Keycloak issuer preflight could not fetch OIDC metadata for realm '{Realm}'. "
                + "Skipping issuer check; the API will start but tokens may fail to validate.",
                _options.OwnerRealm);
            return;
        }

        if (string.Equals(configuration.Issuer, expectedIssuer, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Keycloak issuer preflight passed. Expected and actual issuer: {Issuer}",
                expectedIssuer);
            return;
        }

        string message =
            $"Keycloak issuer mismatch. Expected {expectedIssuer} but metadata returned {configuration.Issuer}.";

        if (_environment.IsProduction())
        {
            _logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        _logger.LogWarning(message);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
