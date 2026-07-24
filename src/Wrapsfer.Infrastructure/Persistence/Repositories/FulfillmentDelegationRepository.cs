using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class FulfillmentDelegationRepository(ApplicationDbContext context)
    : IFulfillmentDelegationRepository
{
    public async Task<FulfillmentDelegation?> GetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.FulfillmentDelegations
            .FirstOrDefaultAsync(d => d.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<FulfillmentDelegation>> ListAsync(
        FulfillmentDelegationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<FulfillmentDelegation> query = context.FulfillmentDelegations;

        if (status is FulfillmentDelegationStatus filter)
        {
            query = query.Where(d => d.Status == filter);
        }

        return await query
            .OrderByDescending(d => d.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(FulfillmentDelegation delegation) =>
        context.FulfillmentDelegations.Add(delegation);
}
