using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

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

    public async Task<IReadOnlyDictionary<Guid, StockAlertLog>> GetLastSentAlertsByProductIdsAsync(
        string tenantId,
        IEnumerable<Guid> productIds,
        StockAlertType alertType,
        CancellationToken cancellationToken = default)
    {
        List<Guid> idList = productIds.ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, StockAlertLog>();
        }

        List<StockAlertLog> logs = await context.StockAlertLogs
            .Where(l => l.TenantId == tenantId
                        && idList.Contains(l.ProductId)
                        && l.AlertType == alertType
                        && l.DeliveryStatus == StockAlertDeliveryStatus.Sent)
            .ToListAsync(cancellationToken);

        return logs
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.OccurredOn).First());
    }

    public void Add(StockAlertLog log) => context.StockAlertLogs.Add(log);
}
