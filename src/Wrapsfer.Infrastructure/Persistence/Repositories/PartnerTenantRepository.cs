using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class PartnerTenantRepository(ApplicationDbContext context) : IPartnerTenantRepository
{
    public async Task<PartnerTenant?>
        GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default) =>
        await context.PartnerTenants
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

    public async Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken = default) =>
        await context.PartnerTenants
            .AnyAsync(p => p.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<PartnerTenant>> ListAsync(bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PartnerTenant> query = context.PartnerTenants;

        if (isActive is bool filter)
        {
            query = query.Where(p => p.IsActive == filter);
        }

        return await query
            .OrderBy(p => p.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public void Add(PartnerTenant partnerTenant) => context.PartnerTenants.Add(partnerTenant);
}
