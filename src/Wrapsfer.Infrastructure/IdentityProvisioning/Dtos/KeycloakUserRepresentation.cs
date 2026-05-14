using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

internal sealed class KeycloakUserRepresentation
{
    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("username")] public string Username { get; set; } = null!;

    [JsonPropertyName("email")] public string Email { get; set; } = null!;

    [JsonPropertyName("emailVerified")] public bool EmailVerified { get; set; }

    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

    [JsonPropertyName("firstName")] public string? FirstName { get; set; }

    [JsonPropertyName("lastName")] public string? LastName { get; set; }

    [JsonPropertyName("credentials")] public List<KeycloakCredentialRepresentation>? Credentials { get; set; }

    [JsonPropertyName("requiredActions")] public List<string>? RequiredActions { get; set; }

    [JsonPropertyName("attributes")] public Dictionary<string, List<string>>? Attributes { get; set; }
}
