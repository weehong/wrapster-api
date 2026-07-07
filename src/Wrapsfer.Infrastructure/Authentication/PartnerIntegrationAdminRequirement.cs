using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class PartnerIntegrationAdminRequirement : IAuthorizationRequirement
{
    public const string TenantIdRouteKey = "tenantId";
}
