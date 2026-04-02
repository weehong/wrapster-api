using System.Security.Claims;
using System.Text.Json;

namespace Wrapster.Application.Tests.Authentication;

public class HttpTenantContextTests
{
    [Fact]
    public void TenantId_WhenRealmIsSet_ReturnsRealm()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantRealm"] = "partner-x";
        var accessor = CreateAccessor(httpContext);

        var tenantContext = new HttpTenantContext(accessor);

        tenantContext.TenantId.Should().Be("partner-x");
    }

    [Fact]
    public void TenantId_WhenRealmNotSet_ThrowsInvalidOperationException()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = CreateAccessor(httpContext);

        var tenantContext = new HttpTenantContext(accessor);

        var act = () => tenantContext.TenantId;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UserId_WhenSubClaimPresent_ReturnsValue()
    {
        var httpContext = new DefaultHttpContext();
        Claim[] claims = new[] { new Claim("sub", "user-123") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = CreateAccessor(httpContext);

        var tenantContext = new HttpTenantContext(accessor);

        tenantContext.UserId.Should().Be("user-123");
    }

    [Fact]
    public void UserId_WhenNoSubClaim_ThrowsInvalidOperationException()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        var accessor = CreateAccessor(httpContext);

        var tenantContext = new HttpTenantContext(accessor);

        var act = () => tenantContext.UserId;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Roles_WhenRealmAccessPresent_ReturnsRoles()
    {
        var httpContext = new DefaultHttpContext();
        string realmAccess = JsonSerializer.Serialize(new
        {
            roles = new[] { "admin", "user" }
        });
        Claim[] claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("realm_access", realmAccess)
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = CreateAccessor(httpContext);

        var tenantContext = new HttpTenantContext(accessor);

        tenantContext.Roles.Should().BeEquivalentTo(["admin", "user"]);
    }

    [Fact]
    public void Roles_WhenNoRealmAccessClaim_ReturnsEmpty()
    {
        var httpContext = new DefaultHttpContext();
        Claim[] claims = new[] { new Claim("sub", "user-123") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = CreateAccessor(httpContext);

        var tenantContext = new HttpTenantContext(accessor);

        tenantContext.Roles.Should().BeEmpty();
    }

    [Fact]
    public void Roles_WhenRealmAccessMalformed_ReturnsEmpty()
    {
        var httpContext = new DefaultHttpContext();
        Claim[] claims =
        [
            new Claim("sub", "user-123"),
            new Claim("realm_access", "{\"roles\": [\"admin\"")
        ];
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = CreateAccessor(httpContext);

        var tenantContext = new HttpTenantContext(accessor);

        tenantContext.Roles.Should().BeEmpty();
    }

    private static IHttpContextAccessor CreateAccessor(HttpContext httpContext) =>
        new HttpContextAccessor { HttpContext = httpContext };
}
