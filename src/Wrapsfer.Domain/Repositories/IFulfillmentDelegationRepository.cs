using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IFulfillmentDelegationRepository
{
    Task<FulfillmentDelegation?> GetByTenantIdAsync(string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists delegations across all tenants, optionally filtered by status. Owner-realm
    /// administration only — partner-facing handlers must always go through
    /// <see cref="GetByTenantIdAsync"/> with their own tenant.
    /// </summary>
    Task<IReadOnlyList<FulfillmentDelegation>> ListAsync(FulfillmentDelegationStatus? status = null,
        CancellationToken cancellationToken = default);

    void Add(FulfillmentDelegation delegation);
}
