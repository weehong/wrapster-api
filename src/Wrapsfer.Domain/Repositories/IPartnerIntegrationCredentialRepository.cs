using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IPartnerIntegrationCredentialRepository
{
    Task<PartnerIntegrationCredential?> GetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<PartnerIntegrationCredential?> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    void Add(PartnerIntegrationCredential credential);

    void Update(PartnerIntegrationCredential credential);
}
