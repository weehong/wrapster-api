using System.Security.Claims;
using System.Text.Json;

namespace Wrapster.Application.Tests.Authentication;

public class HttpTenantContextTests
{
    [Fact]
    public void TenantId_WhenRealmIsSet_ReturnsRealm()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Items["TenantRealm"] = "partner-x";
        IHttpContextAccessor accessor = CreateAccessor(httpContext);

        HttpTenantContext tenantContext = new(accessor);

        tenantContext.TenantId.Should().Be("partner-x");
    }

    [Fact]
    public void TenantId_WhenRealmNotSet_ThrowsInvalidOperationException()
    {
        DefaultHttpContext httpContext = new();
        IHttpContextAccessor accessor = CreateAccessor(httpContext);

        HttpTenantContext tenantContext = new(accessor);

        Func<string> act = () => tenantContext.TenantId;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UserId_WhenSubClaimPresent_ReturnsValue()
    {
        DefaultHttpContext httpContext = new();
        Claim[] claims = new[] { new Claim("sub", "user-123") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        IHttpContextAccessor accessor = CreateAccessor(httpContext);

        HttpTenantContext tenantContext = new(accessor);

        tenantContext.UserId.Should().Be("user-123");
    }

    [Fact]
    public void UserId_WhenNoSubClaim_ThrowsInvalidOperationException()
    {
        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        IHttpContextAccessor accessor = CreateAccessor(httpContext);

        HttpTenantContext tenantContext = new(accessor);

        Func<string> act = () => tenantContext.UserId;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Roles_WhenRealmAccessPresent_ReturnsRoles()
    {
        DefaultHttpContext httpContext = new();
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
        IHttpContextAccessor accessor = CreateAccessor(httpContext);

        HttpTenantContext tenantContext = new(accessor);

        tenantContext.Roles.Should().BeEquivalentTo("admin", "user");
    }

    [Fact]
    public void Roles_WhenNoRealmAccessClaim_ReturnsEmpty()
    {
        DefaultHttpContext httpContext = new();
        Claim[] claims = new[] { new Claim("sub", "user-123") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        IHttpContextAccessor accessor = CreateAccessor(httpContext);

        HttpTenantContext tenantContext = new(accessor);

        tenantContext.Roles.Should().BeEmpty();
    }

    [Fact]
    public void Roles_WhenRealmAccessMalformed_ReturnsEmpty()
    {
        DefaultHttpContext httpContext = new();
        Claim[] claims =
        [
            new("sub", "user-123"),
            new("realm_access", "{\"roles\": [\"admin\"")
        ];
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        IHttpContextAccessor accessor = CreateAccessor(httpContext);

        HttpTenantContext tenantContext = new(accessor);

        tenantContext.Roles.Should().BeEmpty();
    }

    private static IHttpContextAccessor CreateAccessor(HttpContext httpContext) =>
        new HttpContextAccessor { HttpContext = httpContext };
}
