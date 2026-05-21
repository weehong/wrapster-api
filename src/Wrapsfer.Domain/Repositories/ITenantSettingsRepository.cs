using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int?>> GetDefaultThresholdsByTenantIdsAsync(
        IEnumerable<string> tenantIds,
        CancellationToken cancellationToken = default);

    void Add(TenantSettings settings);
}
