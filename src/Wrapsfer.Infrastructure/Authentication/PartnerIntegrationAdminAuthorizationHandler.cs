using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Wrapsfer.Infrastructure.Authentication;

/// <summary>
/// Grants access to owner-realm admins for any tenant, and to partner-realm admins
/// only when the route's {tenantId} matches their own realm. The realm in
/// HttpContext.Items is trustworthy because token validation pins the JWT issuer
/// to that realm (see <see cref="MultiTenantJwtBearerEvents"/>).
/// </summary>
public sealed class PartnerIntegrationAdminAuthorizationHandler
    : AuthorizationHandler<PartnerIntegrationAdminRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly KeycloakOptions _options;

    public PartnerIntegrationAdminAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<KeycloakOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PartnerIntegrationAdminRequirement requirement)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Task.CompletedTask;
        }

        string? realm = httpContext.Items[KeycloakClaimParser.TenantRealmKey] as string;
        if (string.IsNullOrEmpty(realm))
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<string> roles = KeycloakClaimParser.ExtractRealmRoles(
            context.User.FindFirst(KeycloakClaimParser.RealmAccessClaim)?.Value);

        if (!roles.Any(r => string.Equals(r, OwnerAdminRequirement.AdminRoleName, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        if (string.Equals(realm, _options.OwnerRealm, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        string? routeTenantId =
            httpContext.Request.RouteValues[PartnerIntegrationAdminRequirement.TenantIdRouteKey] as string;

        if (!string.IsNullOrEmpty(routeTenantId)
            && string.Equals(routeTenantId, realm, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
