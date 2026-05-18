using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Wrapsfer.Infrastructure.Authentication;

namespace Wrapsfer.Api.Filters;

public sealed class TenantResolutionFilter(IOptions<KeycloakOptions> options) : IAsyncActionFilter
{
    private const string TenantRealmKey = "TenantRealm";

    /// <summary>
    /// <see cref="HttpContext.Items"/> key set to <c>true</c> when an owner account invokes an
    /// <see cref="AllowOwnerTenantScopeAttribute"/> endpoint without a <c>tenantId</c>, signalling
    /// that the request should span all partner tenants.
    /// </summary>
    public const string OwnerCrossTenantScopeKey = "OwnerCrossTenantScope";

    private readonly string _ownerRealm = options.Value.OwnerRealm;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? currentRealm = context.HttpContext.Items[TenantRealmKey] as string;

        if (string.Equals(currentRealm, _ownerRealm, StringComparison.OrdinalIgnoreCase))
        {
            bool allowOwnerTenantScope = context.ActionDescriptor.EndpointMetadata
                .OfType<AllowOwnerTenantScopeAttribute>()
                .Any();

            if (!context.HttpContext.Request.Query.TryGetValue("tenantId", out StringValues tenantIdValues)
                || string.IsNullOrWhiteSpace(tenantIdValues.ToString()))
            {
                if (allowOwnerTenantScope)
                {
                    context.HttpContext.Items[OwnerCrossTenantScopeKey] = true;
                    await next();
                    return;
                }

                context.Result = new BadRequestObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Tenant Required",
                    Detail =
                        "Owner accounts must specify a tenantId query parameter to act on behalf of a partner."
                });
                return;
            }

            string tenantId = tenantIdValues.ToString();

            if (string.Equals(tenantId, _ownerRealm, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new BadRequestObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Tenant",
                    Detail = "Owner accounts cannot operate on the owner tenant. Specify a partner tenant."
                });
                return;
            }

            context.HttpContext.Items[TenantRealmKey] = tenantId;
        }

        await next();
    }
}
