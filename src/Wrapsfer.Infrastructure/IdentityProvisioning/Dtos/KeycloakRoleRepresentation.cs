using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

internal sealed class KeycloakRoleRepresentation
{
    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = null!;

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("clientRole")] public bool ClientRole { get; set; }

    [JsonPropertyName("composite")] public bool Composite { get; set; }
}
