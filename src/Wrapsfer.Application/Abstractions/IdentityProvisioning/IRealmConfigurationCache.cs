namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public interface IRealmConfigurationCache
{
    void Remove(string realm);
}
