using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IStockAlertLogRepository
{
    Task<StockAlertLog?> GetLastAlertAsync(
        string tenantId,
        Guid productId,
        StockAlertType alertType,
        StockAlertDeliveryStatus deliveryStatus,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<StockAlertLog> Items, int TotalCount)> ListAsync(
        string tenantId,
        Guid? productId,
        StockAlertType? alertType,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    void Add(StockAlertLog log);
}
