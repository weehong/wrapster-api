using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class ShopeeProductLinkRepository(ApplicationDbContext context)
    : IShopeeProductLinkRepository
{
    public async Task<ShopeeProductLink?> GetByIdAsync(
        Guid id,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeProductLinks
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<ShopeeProductLink>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeProductLinks
            .Where(l => l.TenantId == tenantId)
            .OrderBy(l => l.ShopeeItemName)
            .ThenBy(l => l.ShopeeModelName)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ShopeeProductLink>> ListByShopeeItemIdsAsync(
        string tenantId,
        IReadOnlyCollection<long> itemIds,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeProductLinks
            .Where(l => l.TenantId == tenantId && itemIds.Contains(l.ShopeeItemId))
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(
        string tenantId,
        long shopeeItemId,
        long shopeeModelId,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeProductLinks.AnyAsync(
            l => l.TenantId == tenantId
                 && l.ShopeeItemId == shopeeItemId
                 && l.ShopeeModelId == shopeeModelId,
            cancellationToken);

    public async Task<bool> ExistsForProductAsync(
        string tenantId,
        Guid productId,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeProductLinks.AnyAsync(
            l => l.TenantId == tenantId && l.ProductId == productId,
            cancellationToken);

    public async Task<IReadOnlyList<ShopeeProductLink>> ListPendingSyncAsync(
        DateTime asOf,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeProductLinks
            .Join(
                context.Products,
                link => new { link.TenantId, link.ProductId },
                product => new { product.TenantId, ProductId = product.Id },
                (link, product) => new
                {
                    Link = link,
                    TargetQuantity = product.IsActive
                        ? Math.Max(product.StockQuantity - product.ReservedQuantity, 0)
                        : 0
                })
            .Where(row =>
                (row.Link.LastSyncedAt == null
                 || row.Link.LastSyncedQuantity == null
                 || row.TargetQuantity != row.Link.LastSyncedQuantity)
                && (row.Link.NextSyncEligibleAt == null || row.Link.NextSyncEligibleAt <= asOf))
            .OrderBy(row => row.Link.LastSyncAttemptedAt.HasValue)
            .ThenBy(row => row.Link.LastSyncAttemptedAt)
            .Select(row => row.Link)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(ShopeeProductLink link) =>
        context.ShopeeProductLinks.Add(link);

    public void Remove(ShopeeProductLink link) =>
        context.ShopeeProductLinks.Remove(link);

    public void RemoveRange(IReadOnlyCollection<ShopeeProductLink> links) =>
        context.ShopeeProductLinks.RemoveRange(links);
}
