using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class StockMovementRepository(ApplicationDbContext context) : IStockMovementRepository
{
    public async Task<IReadOnlyList<ProductStockSnapshot>> GetPointInTimeSnapshotAsync(
        string tenantId,
        DateTime asOfUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        // Postgres DISTINCT ON keeps one row per product — the latest movement before the cutoff.
        List<StockMovement> latestMovements = await context.StockMovements
            .FromSql(
                $"""
                 SELECT DISTINCT ON (sm."ProductId") sm.*
                 FROM "StockMovements" sm
                 WHERE sm."TenantId" = {tenantId} AND sm."OccurredAt" < {asOfUtcExclusive}
                 ORDER BY sm."ProductId", sm."OccurredAt" DESC
                 """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (latestMovements.Count == 0)
        {
            return [];
        }

        List<Guid> productIds = latestMovements.Select(m => m.ProductId).ToList();

        List<Product> products = await context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                        && productIds.Contains(p.Id)
                        && p.Type != ProductType.Bundle)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);

        List<ProductStockSnapshot> snapshots = [];
        foreach (StockMovement movement in latestMovements)
        {
            if (!productsById.TryGetValue(movement.ProductId, out Product? product))
            {
                continue;
            }

            snapshots.Add(new ProductStockSnapshot(
                product.Id,
                product.Barcode,
                product.SkuCode,
                product.Name,
                movement.QuantityAfter,
                movement.UnitCost));
        }

        return snapshots
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
