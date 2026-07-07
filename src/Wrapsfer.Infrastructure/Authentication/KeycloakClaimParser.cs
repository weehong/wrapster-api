using System.Text.Json;

namespace Wrapsfer.Infrastructure.Authentication;

internal static class KeycloakClaimParser
{
    internal const string TenantRealmKey = "TenantRealm";
    internal const string RealmAccessClaim = "realm_access";

    /// <summary>
    /// Extracts the realm name from a Keycloak issuer claim of the form
    /// <c>{baseUrl}/realms/{realm}</c>. Returns <c>null</c> when the issuer is missing or unparseable.
    /// </summary>
    internal static string? ExtractRealmFromIssuer(string? issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return null;
        }

        const string Marker = "/realms/";
        int markerIndex = issuer.LastIndexOf(Marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        string realm = issuer[(markerIndex + Marker.Length)..].Trim('/');
        return string.IsNullOrWhiteSpace(realm) ? null : realm;
    }

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
