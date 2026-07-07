using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class IntegrationApiRequirement : IAuthorizationRequirement
{
    public const string IntegrationRoleName = "integration_api";
}
