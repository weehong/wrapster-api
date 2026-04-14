using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    void Add(TenantSettings settings);
}
