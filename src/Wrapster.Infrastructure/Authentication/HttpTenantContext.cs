using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Wrapster.Application.Abstractions;

namespace Wrapster.Infrastructure.Authentication;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string TenantId =>
        _httpContextAccessor.HttpContext?.Items["TenantRealm"] as string
        ?? throw new InvalidOperationException("Tenant realm has not been resolved.");

    public string UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User is not authenticated.");

    public IReadOnlyList<string> Roles
    {
        get
        {
            string? realmAccessClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue("realm_access");

            if (string.IsNullOrEmpty(realmAccessClaim))
            {
                return Array.Empty<string>();
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(realmAccessClaim);

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
                return Array.Empty<string>();
            }

            return Array.Empty<string>();
        }
    }
}
