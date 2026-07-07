using Microsoft.AspNetCore.Authorization;
using Wrapsfer.Application.Common;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class OwnerAdminRequirement : IAuthorizationRequirement
{
    public const string AdminRoleName = TenantRoles.Admin;
}
