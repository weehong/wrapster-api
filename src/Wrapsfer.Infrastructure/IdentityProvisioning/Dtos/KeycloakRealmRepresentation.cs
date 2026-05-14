using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

internal sealed class KeycloakRealmRepresentation
{
    [JsonPropertyName("realm")] public string Realm { get; set; } = null!;

    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }

    [JsonPropertyName("enabled")] public bool Enabled { get; set; }

    [JsonPropertyName("loginWithEmailAllowed")]
    public bool LoginWithEmailAllowed { get; set; } = true;

    [JsonPropertyName("duplicateEmailsAllowed")]
    public bool DuplicateEmailsAllowed { get; set; }

    [JsonPropertyName("resetPasswordAllowed")]
    public bool ResetPasswordAllowed { get; set; } = true;

    [JsonPropertyName("bruteForceProtected")]
    public bool BruteForceProtected { get; set; } = true;
}
