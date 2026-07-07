using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class OwnerAdminAuthorizationHandler : AuthorizationHandler<OwnerAdminRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly KeycloakOptions _options;

    public OwnerAdminAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<KeycloakOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerAdminRequirement requirement)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Task.CompletedTask;
        }

        string? realm = httpContext.Items[KeycloakClaimParser.TenantRealmKey] as string;
        if (!string.Equals(realm, _options.OwnerRealm, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<string> roles = KeycloakClaimParser.ExtractRealmRoles(
            context.User.FindFirst(KeycloakClaimParser.RealmAccessClaim)?.Value);

        if (roles.Any(r => string.Equals(r, OwnerAdminRequirement.AdminRoleName, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
