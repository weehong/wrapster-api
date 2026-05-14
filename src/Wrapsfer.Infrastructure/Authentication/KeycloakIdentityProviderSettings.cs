using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;

namespace Wrapsfer.Infrastructure.Authentication;

internal sealed class KeycloakIdentityProviderSettings : IIdentityProviderSettings
{
    private readonly KeycloakOptions _options;

    public KeycloakIdentityProviderSettings(IOptions<KeycloakOptions> options) => _options = options.Value;

    public string OwnerRealm => _options.OwnerRealm;
}
