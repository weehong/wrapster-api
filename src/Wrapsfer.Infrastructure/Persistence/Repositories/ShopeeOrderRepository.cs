using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class ShopeeOrderRepository(ApplicationDbContext context) : IShopeeOrderRepository
{
    public async Task<ShopeeOrder?> GetByIdAsync(
        Guid id,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId, cancellationToken);

    public async Task<ShopeeOrder?> GetByOrderSnAsync(
        string orderSn,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderSn == orderSn && o.TenantId == tenantId, cancellationToken);

    public async Task<(IReadOnlyList<ShopeeOrder> Items, int TotalCount)> ListAsync(
        string tenantId,
        ShopeeOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ShopeeOrder> query = context.ShopeeOrders
            .Where(o => o.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderSn, pattern) ||
                (o.TrackingNumber != null && EF.Functions.ILike(o.TrackingNumber, pattern)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        IReadOnlyList<ShopeeOrder> items = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ShopeeOrder>> ListAwaitingTrackingAsync(
        DateTime arrangedBefore,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeOrders
            .Include(o => o.Items)
            .Where(o => o.Status == ShopeeOrderStatus.AwaitingTracking
                        && o.ShipmentArrangedAt != null
                        && o.ShipmentArrangedAt < arrangedBefore)
            .OrderBy(o => o.ShipmentArrangedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(ShopeeOrder order) =>
        context.ShopeeOrders.Add(order);
}
