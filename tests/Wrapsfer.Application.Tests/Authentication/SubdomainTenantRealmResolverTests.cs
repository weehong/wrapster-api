namespace Wrapsfer.Application.Tests.Authentication;

public class SubdomainTenantRealmResolverTests
{
    private const string OwnerRealm = "owner";
    private readonly SubdomainTenantRealmResolver _resolver;

    public SubdomainTenantRealmResolverTests()
    {
        IOptions<KeycloakOptions> options = Options.Create(new KeycloakOptions
        {
            BaseUrl = "https://auth.domain.com",
            OwnerRealm = OwnerRealm,
            Audience = "wrapsfer-api"
        });

        _resolver = new SubdomainTenantRealmResolver(options);
    }

    [Fact]
    public void ResolveRealm_AppSubdomain_ReturnsOwnerRealm()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("app.domain.com");

        string realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_PartnerSubdomain_ReturnsSubdomain()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("partner-x.domain.com");

        string realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be("partner-x");
    }

    [Fact]
    public void ResolveRealm_Localhost_ReturnsOwnerRealm()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("localhost");

        string realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_LocalhostWithPort_ReturnsOwnerRealm()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("localhost", 5001);

        string realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_TwoPartHost_ReturnsOwnerRealm()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("domain.com");

        string realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }

    [Fact]
    public void ResolveRealm_EmptyHost_ThrowsInvalidOperationException()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString(string.Empty);

        Func<string> act = () => _resolver.ResolveRealm(context.Request);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveRealm_AppSubdomain_CaseInsensitive()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("APP.domain.com");

        string realm = _resolver.ResolveRealm(context.Request);

        realm.Should().Be(OwnerRealm);
    }
}
