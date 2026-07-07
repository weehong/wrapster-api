using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class PartnerBillingCustomerRepository(ApplicationDbContext context)
    : IPartnerBillingCustomerRepository
{
    public async Task<PartnerBillingCustomer?> GetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PartnerBillingCustomers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

    public void Add(PartnerBillingCustomer customer) => context.PartnerBillingCustomers.Add(customer);
}
