using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IPartnerBillingCustomerRepository
{
    Task<PartnerBillingCustomer?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);

    void Add(PartnerBillingCustomer customer);
}
