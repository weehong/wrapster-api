using System.Text.Json;

namespace Wrapster.Infrastructure.Authentication;

internal static class KeycloakClaimParser
{
    internal const string TenantRealmKey = "TenantRealm";
    internal const string RealmAccessClaim = "realm_access";

    internal static IReadOnlyList<string> ExtractRealmRoles(string? realmAccessJson)
    {
        if (string.IsNullOrEmpty(realmAccessJson))
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(realmAccessJson);

            if (document.RootElement.TryGetProperty("roles", out JsonElement rolesElement)
                && rolesElement.ValueKind == JsonValueKind.Array)
            {
                return rolesElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList()
                    .AsReadOnly();
            }
        }
        catch (JsonException)
        {
        }

        return [];
    }
}
