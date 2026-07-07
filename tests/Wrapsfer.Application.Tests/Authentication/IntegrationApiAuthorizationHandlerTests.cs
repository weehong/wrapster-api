using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Application.Tests.Authentication;

public class IntegrationApiAuthorizationHandlerTests
{
    [Fact]
    public async Task Handle_WhenIntegrationApiRole_Succeeds()
    {
        AuthorizationHandlerContext context = CreateContext(new[] { "integration_api" });
        IntegrationApiAuthorizationHandler handler = new();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNoIntegrationApiRole_DoesNotSucceed()
    {
        AuthorizationHandlerContext context = CreateContext(new[] { "admin", "user" });
        IntegrationApiAuthorizationHandler handler = new();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoRealmAccessClaim_DoesNotSucceed()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test"));
        AuthorizationHandlerContext context = new(new[] { new IntegrationApiRequirement() }, user, null);
        IntegrationApiAuthorizationHandler handler = new();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext CreateContext(IReadOnlyList<string> roles)
    {
        string realmAccess = JsonSerializer.Serialize(new { roles });
        Claim[] claims =
        [
            new("sub", "service-account-wrapsfer-partner-acme-integration"),
            new("realm_access", realmAccess)
        ];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));

        IntegrationApiRequirement requirement = new();
        return new AuthorizationHandlerContext(new[] { requirement }, user, null);
    }
}
