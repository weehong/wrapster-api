using Microsoft.EntityFrameworkCore;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Repositories;

namespace Wrapster.Infrastructure.Persistence.Repositories;

internal sealed class StockAlertLogRepository(ApplicationDbContext context) : IStockAlertLogRepository
{
    public async Task<StockAlertLog?> GetLastAlertAsync(
        string tenantId,
        Guid productId,
        StockAlertType alertType,
        StockAlertDeliveryStatus deliveryStatus,
        CancellationToken cancellationToken = default) =>
        await context.StockAlertLogs
            .Where(l => l.TenantId == tenantId
                        && l.ProductId == productId
                        && l.AlertType == alertType
                        && l.DeliveryStatus == deliveryStatus)
            .OrderByDescending(l => l.OccurredOn)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<StockAlertLog> Items, int TotalCount)> ListAsync(
        string tenantId,
        Guid? productId,
        StockAlertType? alertType,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StockAlertLog> query = context.StockAlertLogs
            .Where(l => l.TenantId == tenantId);

        if (productId.HasValue)
        {
            query = query.Where(l => l.ProductId == productId.Value);
        }

        if (alertType.HasValue)
        {
            query = query.Where(l => l.AlertType == alertType.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(l => l.OccurredOn >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.OccurredOn <= to.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<StockAlertLog> items = await query
            .OrderByDescending(l => l.OccurredOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(StockAlertLog log) => context.StockAlertLogs.Add(log);
}
