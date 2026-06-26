using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

namespace Wrapsfer.Infrastructure.IdentityProvisioning;

internal sealed class KeycloakIdentityUserDirectory : IIdentityUserDirectory
{
    private readonly KeycloakAdminHttpClient _adminHttp;
    private readonly ILogger<KeycloakIdentityUserDirectory> _logger;

    public KeycloakIdentityUserDirectory(
        KeycloakAdminHttpClient adminHttp,
        ILogger<KeycloakIdentityUserDirectory> logger)
    {
        _adminHttp = adminHttp;
        _logger = logger;
    }

    public async Task<ResolvedActor?> ResolveActorAsync(
        string realm,
        string userId,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = await _adminHttp.CreateAuthorizedClientAsync(cancellationToken);

        using HttpResponseMessage response = await client.GetAsync(
            $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}",
            cancellationToken);

        // A user that does not exist in this realm is an expected outcome (the caller cascades
        // across candidate realms), not an error.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await _adminHttp.EnsureSuccessAsync(
            response, $"resolve user {userId} in realm {realm}", cancellationToken);

        KeycloakUserRepresentation? user =
            await response.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.Username))
        {
            _logger.LogWarning(
                "Identity provider returned an empty user payload for {UserId} in realm {Realm}.",
                userId, realm);
            return null;
        }

        return new ResolvedActor(user.Username, BuildFullName(user));
    }

    private static string? BuildFullName(KeycloakUserRepresentation user)
    {
        string fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }
}
