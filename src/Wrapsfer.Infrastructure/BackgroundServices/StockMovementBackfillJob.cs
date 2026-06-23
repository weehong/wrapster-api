using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Infrastructure.Persistence;

namespace Wrapsfer.Infrastructure.BackgroundServices;

/// <summary>
/// One-time, idempotent backfill of the inventory ledger from the existing audit log. The
/// <see cref="AuditLog"/> table has recorded every product stock/cost change since the system's
/// first migration, so replaying those entries chronologically reconstructs accurate point-in-time
/// history. Products without audit history (or with none touching stock/cost) get a single baseline
/// row from their current values. Skips entirely once any ledger row exists.
/// </summary>
public sealed class StockMovementBackfillJob(
    IServiceScopeFactory scopeFactory,
    ILogger<StockMovementBackfillJob> logger) : BackgroundService
{
    private const string ProductEntityName = nameof(Product);
    private const int SaveBatchSize = 2000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (await context.StockMovements.AnyAsync(stoppingToken))
            {
                return;
            }

            await BackfillAsync(context, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stock movement backfill failed");
        }
    }

    private async Task BackfillAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        List<Product> products = await context.Products
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<AuditLog> productAudits = await context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == ProductEntityName)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);

        Dictionary<string, List<AuditLog>> auditsByProductId = productAudits
            .GroupBy(a => a.EntityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<StockMovement> movements = [];
        foreach (Product product in products)
        {
            List<StockMovement> productMovements =
                auditsByProductId.TryGetValue(product.Id.ToString(), out List<AuditLog>? audits)
                    ? ReplayProduct(product, audits)
                    : [];

            // Guarantee every product has at least a baseline so point-in-time queries can value it.
            if (productMovements.Count == 0)
            {
                productMovements.Add(BaselineFor(product));
            }

            movements.AddRange(productMovements);
        }

        int inserted = await SaveInBatchesAsync(context, movements, cancellationToken);
        logger.LogInformation(
            "Stock movement backfill complete: inserted {Inserted} ledger rows for {ProductCount} products",
            inserted, products.Count);
    }

    internal static List<StockMovement> ReplayProduct(Product product, List<AuditLog> audits)
    {
        List<StockMovement> movements = [];
        int quantity = 0;
        decimal cost = product.Cost;

        foreach (AuditLog audit in audits)
        {
            (bool hasStock, int? stockOld, int? stockNew) = ExtractInt(audit.Changes, nameof(Product.StockQuantity));
            (bool hasCost, _, decimal? costNew) = ExtractDecimal(audit.Changes, nameof(Product.Cost));

            if (hasCost && costNew.HasValue)
            {
                cost = costNew.Value;
            }

            if (audit.Action == AuditAction.Created)
            {
                quantity = stockNew ?? 0;
                movements.Add(StockMovement.Record(
                    product.TenantId, product.Id, StockMovementType.Created,
                    quantity, quantity, cost, audit.Timestamp, audit.UserId));
                continue;
            }

            if (audit.Action == AuditAction.Deleted)
            {
                continue;
            }

            if (hasStock && stockNew.HasValue)
            {
                int previous = stockOld ?? quantity;
                int delta = stockNew.Value - previous;
                quantity = stockNew.Value;
                StockMovementType type = delta >= 0 ? StockMovementType.Increase : StockMovementType.Decrease;
                movements.Add(StockMovement.Record(
                    product.TenantId, product.Id, type,
                    delta, quantity, cost, audit.Timestamp, audit.UserId));
            }
            else if (hasCost && costNew.HasValue)
            {
                movements.Add(StockMovement.Record(
                    product.TenantId, product.Id, StockMovementType.CostAdjustment,
                    0, quantity, cost, audit.Timestamp, audit.UserId));
            }
        }

        return movements;
    }

    private static StockMovement BaselineFor(Product product) =>
        StockMovement.Record(
            product.TenantId,
            product.Id,
            StockMovementType.Baseline,
            product.StockQuantity,
            product.StockQuantity,
            product.Cost,
            product.UpdatedAt ?? product.CreatedAt,
            product.UpdatedBy ?? product.CreatedBy);

    private static async Task<int> SaveInBatchesAsync(
        ApplicationDbContext context, List<StockMovement> movements, CancellationToken cancellationToken)
    {
        int inserted = 0;
        for (int offset = 0; offset < movements.Count; offset += SaveBatchSize)
        {
            List<StockMovement> batch = movements
                .Skip(offset)
                .Take(SaveBatchSize)
                .ToList();

            context.StockMovements.AddRange(batch);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
            inserted += batch.Count;
        }

        return inserted;
    }

    private static (bool Found, int? OldValue, int? NewValue) ExtractInt(string? changesJson, string propertyName)
    {
        if (!TryGetChange(changesJson, propertyName, out JsonElement change))
        {
            return (false, null, null);
        }

        int? oldValue = change.TryGetProperty("OldValue", out JsonElement oldEl) && oldEl.ValueKind == JsonValueKind.Number
            ? oldEl.GetInt32()
            : null;
        int? newValue = change.TryGetProperty("NewValue", out JsonElement newEl) && newEl.ValueKind == JsonValueKind.Number
            ? newEl.GetInt32()
            : null;

        return (true, oldValue, newValue);
    }

    private static (bool Found, decimal? OldValue, decimal? NewValue) ExtractDecimal(
        string? changesJson, string propertyName)
    {
        if (!TryGetChange(changesJson, propertyName, out JsonElement change))
        {
            return (false, null, null);
        }

        decimal? oldValue = change.TryGetProperty("OldValue", out JsonElement oldEl) && oldEl.ValueKind == JsonValueKind.Number
            ? oldEl.GetDecimal()
            : null;
        decimal? newValue = change.TryGetProperty("NewValue", out JsonElement newEl) && newEl.ValueKind == JsonValueKind.Number
            ? newEl.GetDecimal()
            : null;

        return (true, oldValue, newValue);
    }

    private static bool TryGetChange(string? changesJson, string propertyName, out JsonElement change)
    {
        change = default;
        if (string.IsNullOrWhiteSpace(changesJson))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(changesJson);
        if (!document.RootElement.TryGetProperty(propertyName, out JsonElement element))
        {
            return false;
        }

        change = element.Clone();
        return true;
    }
}
