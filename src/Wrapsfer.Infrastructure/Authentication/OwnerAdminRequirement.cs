using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class OwnerAdminRequirement : IAuthorizationRequirement
{
    public const string AdminRoleName = "admin";
}
