using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

internal sealed class KeycloakCredentialRepresentation
{
    [JsonPropertyName("type")] public string Type { get; set; } = "password";

    [JsonPropertyName("value")] public string Value { get; set; } = null!;

    [JsonPropertyName("temporary")] public bool Temporary { get; set; } = true;
}
