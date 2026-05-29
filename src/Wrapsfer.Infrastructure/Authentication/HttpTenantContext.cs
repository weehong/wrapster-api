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

    public string Username =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("preferred_username")
        ?? throw new InvalidOperationException("User is not authenticated.");

    public string? DisplayName =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("name")
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("preferred_username");

    public string? Email =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("email");

    public IReadOnlyList<string> Roles =>
        KeycloakClaimParser.ExtractRealmRoles(
            _httpContextAccessor.HttpContext?.User.FindFirstValue(KeycloakClaimParser.RealmAccessClaim));
}
