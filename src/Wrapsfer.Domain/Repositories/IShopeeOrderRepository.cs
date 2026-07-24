using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IShopeeOrderRepository
{
    Task<ShopeeOrder?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<ShopeeOrder?> GetByOrderSnAsync(string orderSn, string tenantId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ShopeeOrder> Items, int TotalCount)> ListAsync(
        string tenantId,
        ShopeeOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists orders stuck in AwaitingTracking whose shipment was arranged before
    /// <paramref name="arrangedBefore"/>. Intentionally spans all tenants: it exists solely
    /// for the reconciliation background job's tracking-number re-poll, never for
    /// user-driven request handling.
    /// </summary>
    Task<IReadOnlyList<ShopeeOrder>> ListAwaitingTrackingAsync(
        DateTime arrangedBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the id of the Shopee order linked to <paramref name="waybillId"/>, or null
    /// when the waybill did not originate from a Shopee shipment.
    /// </summary>
    Task<Guid?> FindIdByWaybillIdAsync(Guid waybillId, string tenantId,
        CancellationToken cancellationToken = default);

    void Add(ShopeeOrder order);
}
