using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class NotPasswordChangeRequiredRequirement : IAuthorizationRequirement
{
    public const string ClaimType = "requires_password_change";
}
