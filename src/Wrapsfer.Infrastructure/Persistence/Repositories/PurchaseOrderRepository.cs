using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class PurchaseOrderRepository(ApplicationDbContext context) : IPurchaseOrderRepository
{
    public async Task<PurchaseOrder?> GetByIdAsync(Guid id, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);

    public async Task<bool> ExistsByNumberAsync(string poNumber, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PurchaseOrders
            .AnyAsync(p => p.TenantId == tenantId && p.PoNumber == poNumber, cancellationToken);

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> ListAsync(
        string tenantId,
        PurchaseOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PurchaseOrder> query = context.PurchaseOrders
            .Where(p => p.TenantId == tenantId);

        return await ApplyFiltersAndPageAsync(query, status, search, page, pageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> ListByTenantIdsAsync(
        IReadOnlyCollection<string> tenantIds,
        PurchaseOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PurchaseOrder> query = context.PurchaseOrders
            .Where(p => tenantIds.Contains(p.TenantId));

        return await ApplyFiltersAndPageAsync(query, status, search, page, pageSize, cancellationToken);
    }

    public void Add(PurchaseOrder purchaseOrder) => context.PurchaseOrders.Add(purchaseOrder);

    private static async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> ApplyFiltersAndPageAsync(
        IQueryable<PurchaseOrder> query,
        PurchaseOrderStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.PoNumber, pattern) ||
                EF.Functions.ILike(p.ProductName, pattern) ||
                EF.Functions.ILike(p.ProductBarcode, pattern));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<PurchaseOrder> items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
