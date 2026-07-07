using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class TenantSettingsRepository(ApplicationDbContext context) : ITenantSettingsRepository
{
    public async Task<TenantSettings?> GetByTenantIdAsync(string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.TenantSettings
            .Include(t => t.Recipients)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyDictionary<string, int?>> GetDefaultThresholdsByTenantIdsAsync(
        IEnumerable<string> tenantIds,
        CancellationToken cancellationToken = default)
    {
        List<string> idList = tenantIds.ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<string, int?>();
        }

        List<(string TenantId, int? Threshold)> rows = await context.TenantSettings
            .Where(t => idList.Contains(t.TenantId))
            .Select(t => new ValueTuple<string, int?>(t.TenantId, t.DefaultLowStockThreshold))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.TenantId, r => r.Threshold);
    }

    public void Add(TenantSettings settings) => context.TenantSettings.Add(settings);
}
