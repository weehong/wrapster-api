using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IPartnerTenantRepository
{
    Task<PartnerTenant?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartnerTenant>> ListAsync(bool? isActive = null, CancellationToken cancellationToken = default);

    void Add(PartnerTenant partnerTenant);
}
