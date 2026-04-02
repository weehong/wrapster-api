namespace Wrapster.Application.Tests.Authentication;

public class SubdomainTenantRealmResolverTests
{
    private const string OwnerRealm = "owner";
    private readonly SubdomainTenantRealmResolver _resolver;

    public SubdomainTenantRealmResolverTests()
    {
        var options = Options.Create(new KeycloakOptions
        {
            BaseUrl = "https://auth.domain.com",
            OwnerRealm = OwnerRealm,
            Audience = "wrapster-api"
        });

        _resolver = new SubdomainTenantRealmResolver(options);
    }

    [Fact]
    public void ResolveRealm_AppSubdomain_ReturnsOwnerRealm()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("app.domain.com");

        var realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_PartnerSubdomain_ReturnsSubdomain()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("partner-x.domain.com");

        var realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be("partner-x");
    }

    [Fact]
    public void ResolveRealm_Localhost_ReturnsOwnerRealm()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost");

        var realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_LocalhostWithPort_ReturnsOwnerRealm()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost", 5001);

        var realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_TwoPartHost_ReturnsOwnerRealm()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("domain.com");

        var realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_EmptyHost_ThrowsInvalidOperationException()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(string.Empty);

        var act = () => _resolver.ResolveRealm(context.Request);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveRealm_AppSubdomain_CaseInsensitive()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("APP.domain.com");

        var realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }
}
