using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class NotPasswordChangeRequiredHandler : AuthorizationHandler<NotPasswordChangeRequiredRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        NotPasswordChangeRequiredRequirement requirement)
    {
        string? claimValue = context.User
            .FindFirst(NotPasswordChangeRequiredRequirement.ClaimType)?.Value;

        bool requiresChange =
            !string.IsNullOrEmpty(claimValue)
            && bool.TryParse(claimValue, out bool parsed)
            && parsed;

        if (!requiresChange)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.Resource is HttpContext httpContext
            && httpContext.GetEndpoint()?.Metadata.GetMetadata<AllowRestrictedTokenAttribute>() is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
