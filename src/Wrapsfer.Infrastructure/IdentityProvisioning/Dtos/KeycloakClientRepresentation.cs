using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

internal sealed class KeycloakClientRepresentation
{
    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("clientId")] public string ClientId { get; set; } = null!;

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

    [JsonPropertyName("publicClient")] public bool PublicClient { get; set; }

    [JsonPropertyName("clientAuthenticatorType")]
    public string ClientAuthenticatorType { get; set; } = "client-secret";

    [JsonPropertyName("directAccessGrantsEnabled")]
    public bool DirectAccessGrantsEnabled { get; set; } = true;

    [JsonPropertyName("standardFlowEnabled")]
    public bool StandardFlowEnabled { get; set; } = true;

    [JsonPropertyName("implicitFlowEnabled")]
    public bool ImplicitFlowEnabled { get; set; }

    [JsonPropertyName("serviceAccountsEnabled")]
    public bool ServiceAccountsEnabled { get; set; }

    [JsonPropertyName("protocol")] public string Protocol { get; set; } = "openid-connect";

    [JsonPropertyName("redirectUris")] public List<string> RedirectUris { get; set; } = new();

    [JsonPropertyName("webOrigins")] public List<string> WebOrigins { get; set; } = new();

    [JsonPropertyName("defaultClientScopes")]
    public List<string>? DefaultClientScopes { get; set; }

    [JsonPropertyName("protocolMappers")]
    public List<KeycloakProtocolMapperRepresentation>? ProtocolMappers { get; set; }
}
