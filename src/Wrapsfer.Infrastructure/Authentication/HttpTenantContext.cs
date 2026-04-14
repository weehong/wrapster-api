using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wrapsfer.Application.Abstractions;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string TenantId =>
        _httpContextAccessor.HttpContext?.Items[KeycloakClaimParser.TenantRealmKey] as string
        ?? throw new InvalidOperationException("Tenant realm has not been resolved.");

    public string UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User is not authenticated.");

    public IReadOnlyList<string> Roles =>
        KeycloakClaimParser.ExtractRealmRoles(
            _httpContextAccessor.HttpContext?.User.FindFirstValue(KeycloakClaimParser.RealmAccessClaim));
}
