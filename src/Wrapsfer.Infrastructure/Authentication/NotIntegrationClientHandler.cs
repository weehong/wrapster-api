using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Infrastructure.Authentication;

/// <summary>
/// Denies machine-to-machine integration tokens on endpoints meant for human users.
/// Integration clients may only call endpoints protected by the
/// <see cref="AuthorizationPolicies.IntegrationApiOnly" /> policy, which does not
/// carry this requirement.
/// </summary>
public sealed class NotIntegrationClientHandler : AuthorizationHandler<NotIntegrationClientRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        NotIntegrationClientRequirement requirement)
    {
        IReadOnlyList<string> roles = KeycloakClaimParser.ExtractRealmRoles(
            context.User.FindFirst(KeycloakClaimParser.RealmAccessClaim)?.Value);

        bool isIntegrationClient = roles.Any(r => string.Equals(
            r, IntegrationApiRequirement.IntegrationRoleName, StringComparison.OrdinalIgnoreCase));

        if (!isIntegrationClient)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
