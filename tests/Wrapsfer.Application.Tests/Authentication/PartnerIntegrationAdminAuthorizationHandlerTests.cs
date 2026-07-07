using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Wrapsfer.Application.Tests.Authentication;

public class PartnerIntegrationAdminAuthorizationHandlerTests
{
    private const string OwnerRealm = "wrapsfer";

    [Fact]
    public async Task Handle_WhenOwnerRealmAdmin_SucceedsForAnyTenant()
    {
        AuthorizationHandlerContext context = CreateContext(
            OwnerRealm, new[] { "admin" }, routeTenantId: "partner-acme", out HttpContextAccessor accessor);
        PartnerIntegrationAdminAuthorizationHandler handler = CreateHandler(accessor);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenPartnerRealmAdminAndOwnTenantRoute_Succeeds()
    {
        AuthorizationHandlerContext context = CreateContext(
            "partner-acme", new[] { "admin" }, routeTenantId: "partner-acme", out HttpContextAccessor accessor);
        PartnerIntegrationAdminAuthorizationHandler handler = CreateHandler(accessor);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenPartnerRealmAdminAndOtherTenantRoute_DoesNotSucceed()
    {
        AuthorizationHandlerContext context = CreateContext(
            "partner-acme", new[] { "admin" }, routeTenantId: "partner-beta", out HttpContextAccessor accessor);
        PartnerIntegrationAdminAuthorizationHandler handler = CreateHandler(accessor);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenPartnerRealmAdminAndMissingTenantRoute_DoesNotSucceed()
    {
        AuthorizationHandlerContext context = CreateContext(
            "partner-acme", new[] { "admin" }, routeTenantId: null, out HttpContextAccessor accessor);
        PartnerIntegrationAdminAuthorizationHandler handler = CreateHandler(accessor);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenPartnerRealmNonAdmin_DoesNotSucceedForOwnTenant()
    {
        AuthorizationHandlerContext context = CreateContext(
            "partner-acme", new[] { "user" }, routeTenantId: "partner-acme", out HttpContextAccessor accessor);
        PartnerIntegrationAdminAuthorizationHandler handler = CreateHandler(accessor);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenOwnerRealmNonAdmin_DoesNotSucceed()
    {
        AuthorizationHandlerContext context = CreateContext(
            OwnerRealm, new[] { "user" }, routeTenantId: "partner-acme", out HttpContextAccessor accessor);
        PartnerIntegrationAdminAuthorizationHandler handler = CreateHandler(accessor);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoHttpContext_DoesNotSucceed()
    {
        PartnerIntegrationAdminRequirement requirement = new();
        ClaimsPrincipal user = new(new ClaimsIdentity());
        AuthorizationHandlerContext context = new(new[] { requirement }, user, null);
        PartnerIntegrationAdminAuthorizationHandler handler = CreateHandler(new HttpContextAccessor());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext CreateContext(
        string realm,
        IReadOnlyList<string> roles,
        string? routeTenantId,
        out HttpContextAccessor accessor)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Items["TenantRealm"] = realm;

        if (routeTenantId is not null)
        {
            httpContext.Request.RouteValues[PartnerIntegrationAdminRequirement.TenantIdRouteKey] = routeTenantId;
        }

        string realmAccess = JsonSerializer.Serialize(new { roles });
        Claim[] claims =
        [
            new("sub", "user-1"),
            new("realm_access", realmAccess)
        ];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        httpContext.User = user;

        accessor = new HttpContextAccessor { HttpContext = httpContext };

        PartnerIntegrationAdminRequirement requirement = new();
        return new AuthorizationHandlerContext(new[] { requirement }, user, null);
    }

    private static PartnerIntegrationAdminAuthorizationHandler CreateHandler(HttpContextAccessor accessor)
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "http://kc/",
            OwnerRealm = OwnerRealm,
            Audience = "wrapsfer"
        };

        return new PartnerIntegrationAdminAuthorizationHandler(accessor, Options.Create(options));
    }
}
