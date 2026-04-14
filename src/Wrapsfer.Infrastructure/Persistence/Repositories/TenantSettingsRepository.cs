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

    public void Add(TenantSettings settings) => context.TenantSettings.Add(settings);
}
