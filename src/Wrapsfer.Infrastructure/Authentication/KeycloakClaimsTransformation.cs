using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class KeycloakClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ClaimsIdentity? identity = principal.Identity as ClaimsIdentity;
        if (identity is null || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        MapRealmRoles(identity);
        MapStandardClaims(identity);

        return Task.FromResult(principal);
    }

    private static void MapRealmRoles(ClaimsIdentity identity)
    {
        Claim? realmAccessClaim = identity.FindFirst("realm_access");
        if (realmAccessClaim is null)
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(realmAccessClaim.Value);

            if (!document.RootElement.TryGetProperty("roles", out JsonElement rolesElement)
                || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement role in rolesElement.EnumerateArray())
            {
                string? roleValue = role.GetString();
                if (!string.IsNullOrEmpty(roleValue)
                    && !identity.HasClaim(ClaimTypes.Role, roleValue))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                }
            }
        }
        catch (JsonException)
        {
            // Malformed realm_access claim — skip silently
        }
    }

    private static void MapStandardClaims(ClaimsIdentity identity)
    {
        MapClaim(identity, "preferred_username", ClaimTypes.Name);
        MapClaim(identity, "email", ClaimTypes.Email);
    }

    private static void MapClaim(ClaimsIdentity identity, string sourceClaim, string targetClaim)
    {
        if (identity.FindFirst(targetClaim) is not null)
        {
            return;
        }

        Claim? source = identity.FindFirst(sourceClaim);
        if (source is not null)
        {
            identity.AddClaim(new Claim(targetClaim, source.Value));
        }
    }
}
