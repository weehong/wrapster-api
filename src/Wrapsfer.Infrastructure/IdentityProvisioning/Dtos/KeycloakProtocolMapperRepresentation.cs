using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

internal sealed class KeycloakProtocolMapperRepresentation
{
    [JsonPropertyName("name")] public string Name { get; set; } = null!;

    [JsonPropertyName("protocol")] public string Protocol { get; set; } = "openid-connect";

    [JsonPropertyName("protocolMapper")] public string ProtocolMapper { get; set; } = null!;

    [JsonPropertyName("consentRequired")] public bool ConsentRequired { get; set; }

    [JsonPropertyName("config")] public Dictionary<string, string> Config { get; set; } = new();
}
