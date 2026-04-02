using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Wrapster.Infrastructure.Authentication;

public sealed class SubdomainTenantRealmResolver : ITenantRealmResolver
{
    private readonly KeycloakOptions _options;

    public SubdomainTenantRealmResolver(IOptions<KeycloakOptions> options) => _options = options.Value;

    public string ResolveRealm(HttpRequest request)
    {
        string host = request.Host.Host;

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Unable to resolve " +
                                                "tenant: Host header is missing or empty.");
        }

        if (!host.Contains("."))
        {
            return _options.OwnerRealm;
        }

        string[] parts = host.Split(".");

        if (parts.Length < 3)
        {
            return _options.OwnerRealm;
        }

        string subdomain = parts[0];

        if (string.Equals(subdomain, "app", StringComparison.OrdinalIgnoreCase))
        {
            return _options.OwnerRealm;
        }

        return subdomain;
    }
}
