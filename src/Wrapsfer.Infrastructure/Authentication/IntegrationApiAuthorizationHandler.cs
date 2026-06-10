using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class IntegrationApiAuthorizationHandler : AuthorizationHandler<IntegrationApiRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IntegrationApiRequirement requirement)
    {
        IReadOnlyList<string> roles = KeycloakClaimParser.ExtractRealmRoles(
            context.User.FindFirst(KeycloakClaimParser.RealmAccessClaim)?.Value);

        if (roles.Any(r => string.Equals(
                r, IntegrationApiRequirement.IntegrationRoleName, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
