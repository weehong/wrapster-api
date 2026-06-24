using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNumberAsync(string poNumber, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNumberAsync(string poNumber, Guid excludeId, string tenantId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> ListAsync(
        string tenantId,
        PurchaseOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> ListByTenantIdsAsync(
        IReadOnlyCollection<string> tenantIds,
        PurchaseOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    void Add(PurchaseOrder purchaseOrder);
}
