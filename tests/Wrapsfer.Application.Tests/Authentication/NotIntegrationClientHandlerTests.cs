using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Application.Tests.Authentication;

public class NotIntegrationClientHandlerTests
{
    [Fact]
    public async Task Handle_WhenHumanUser_Succeeds()
    {
        AuthorizationHandlerContext context = CreateContext(new[] { "admin", "user" });
        NotIntegrationClientHandler handler = new();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNoRealmAccessClaim_Succeeds()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test"));
        AuthorizationHandlerContext context = new(new[] { new NotIntegrationClientRequirement() }, user, null);
        NotIntegrationClientHandler handler = new();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenIntegrationClient_DoesNotSucceed()
    {
        AuthorizationHandlerContext context = CreateContext(new[] { "integration_api" });
        NotIntegrationClientHandler handler = new();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenIntegrationClientWithOtherRoles_DoesNotSucceed()
    {
        AuthorizationHandlerContext context = CreateContext(new[] { "user", "integration_api" });
        NotIntegrationClientHandler handler = new();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext CreateContext(IReadOnlyList<string> roles)
    {
        string realmAccess = JsonSerializer.Serialize(new { roles });
        Claim[] claims =
        [
            new("sub", "user-1"),
            new("realm_access", realmAccess)
        ];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));

        NotIntegrationClientRequirement requirement = new();
        return new AuthorizationHandlerContext(new[] { requirement }, user, null);
    }
}
