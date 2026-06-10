namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public interface IIdentityProviderSettings
{
    string OwnerRealm { get; }

    string GetTokenUrl(string tenantId);
}
