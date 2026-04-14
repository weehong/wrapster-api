using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class RealmConfigurationCache
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = new();
    private readonly KeycloakOptions _options;

    public RealmConfigurationCache(
        IOptions<KeycloakOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(string
        realm, CancellationToken cancellationToken = default)
    {
        ConfigurationManager<OpenIdConnectConfiguration>? manager =
            _managers.GetOrAdd(realm, CreateConfigurationManager);
        return manager.GetConfigurationAsync(cancellationToken);
    }

    private ConfigurationManager<OpenIdConnectConfiguration>
        CreateConfigurationManager(string realm)
    {
        string metadataAddress =
            $"{_options.BaseUrl.TrimEnd('/')}/realms/{realm}/.well-known/openid-configuration";

        HttpClient httpClient = _httpClientFactory.CreateClient("KeycloakOidc");

        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(httpClient)
            {
                RequireHttps = _options.RequireHttpsMetadata
            });
    }
}
