using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IShopeeProductLinkRepository
{
    Task<ShopeeProductLink?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopeeProductLink>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopeeProductLink>> ListByShopeeItemIdsAsync(
        string tenantId,
        IReadOnlyCollection<long> itemIds,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string tenantId,
        long shopeeItemId,
        long shopeeModelId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForProductAsync(
        string tenantId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopeeProductLink>> ListPendingSyncAsync(
        DateTime asOf,
        int batchSize,
        CancellationToken cancellationToken = default);

    void Add(ShopeeProductLink link);

    void Remove(ShopeeProductLink link);

    void RemoveRange(IReadOnlyCollection<ShopeeProductLink> links);
}
