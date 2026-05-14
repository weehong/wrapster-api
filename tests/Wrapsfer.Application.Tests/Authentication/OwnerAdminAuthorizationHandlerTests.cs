using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Application.Tests.Authentication;

public class OwnerAdminAuthorizationHandlerTests
{
    [Fact]
    public async Task Handle_WhenOwnerRealmAndAdminRole_Succeeds()
    {
        AuthorizationHandlerContext context =
            CreateContext("owner", new[] { "admin" }, out HttpContextAccessor accessor);
        OwnerAdminAuthorizationHandler handler = CreateHandler(accessor, "owner");

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenOwnerRealmAndNoAdminRole_DoesNotSucceed()
    {
        AuthorizationHandlerContext
            context = CreateContext("owner", new[] { "user" }, out HttpContextAccessor accessor);
        OwnerAdminAuthorizationHandler handler = CreateHandler(accessor, "owner");

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenPartnerRealmEvenWithAdminRole_DoesNotSucceed()
    {
        AuthorizationHandlerContext context =
            CreateContext("partner-acme", new[] { "admin" }, out HttpContextAccessor accessor);
        OwnerAdminAuthorizationHandler handler = CreateHandler(accessor, "owner");

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoHttpContext_DoesNotSucceed()
    {
        OwnerAdminRequirement requirement = new();
        ClaimsPrincipal user = new(new ClaimsIdentity());
        AuthorizationHandlerContext context = new(new[] { requirement }, user, null);
        OwnerAdminAuthorizationHandler handler = new(
            new HttpContextAccessor(),
            Options.Create(new KeycloakOptions
            { BaseUrl = "http://kc/", OwnerRealm = "owner", Audience = "wrapsfer-api" }));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext CreateContext(
        string realm,
        IReadOnlyList<string> roles,
        out HttpContextAccessor accessor)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Items["TenantRealm"] = realm;

        string realmAccess = JsonSerializer.Serialize(new { roles });
        Claim[] claims =
        [
            new("sub", "user-1"),
            new("realm_access", realmAccess)
        ];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        httpContext.User = user;

        accessor = new HttpContextAccessor { HttpContext = httpContext };

        OwnerAdminRequirement requirement = new();
        return new AuthorizationHandlerContext(new[] { requirement }, user, null);
    }

    private static OwnerAdminAuthorizationHandler CreateHandler(HttpContextAccessor accessor, string ownerRealm)
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "http://kc/",
            OwnerRealm = ownerRealm,
            Audience = "wrapsfer-api"
        };

        return new OwnerAdminAuthorizationHandler(accessor, Options.Create(options));
    }
}
