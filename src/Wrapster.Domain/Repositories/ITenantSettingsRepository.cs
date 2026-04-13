using Wrapster.Domain.Entities;

namespace Wrapster.Domain.Repositories;

public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    void Add(TenantSettings settings);
}
