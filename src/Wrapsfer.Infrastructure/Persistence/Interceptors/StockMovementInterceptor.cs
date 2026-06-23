using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Records an inventory ledger row whenever a product's stock quantity or cost changes.
/// Runs before SaveChanges (like <see cref="AuditLogInterceptor"/>) so the movement is written in
/// the same transaction as the product change. The tenant is taken from the product itself; only the
/// acting user is resolved from the request context.
/// </summary>
public sealed class StockMovementInterceptor(ITenantContext? tenantContext = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RecordMovements(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RecordMovements(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void RecordMovements(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        string? userId = ResolveUserId();
        DateTime utcNow = DateTime.UtcNow;

        List<EntityEntry<Product>> entries = context.ChangeTracker.Entries<Product>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        foreach (EntityEntry<Product> entry in entries)
        {
            StockMovement? movement = BuildMovement(entry, userId, utcNow);
            if (movement is not null)
            {
                context.Add(movement);
            }
        }
    }

    private static StockMovement? BuildMovement(EntityEntry<Product> entry, string? userId, DateTime utcNow)
    {
        Product product = entry.Entity;

        if (entry.State == EntityState.Added)
        {
            return StockMovement.Record(
                product.TenantId,
                product.Id,
                StockMovementType.Created,
                product.StockQuantity,
                product.StockQuantity,
                product.Cost,
                utcNow,
                userId);
        }

        PropertyEntry<Product, int> stockProperty = entry.Property(p => p.StockQuantity);
        PropertyEntry<Product, decimal> costProperty = entry.Property(p => p.Cost);

        bool stockChanged = stockProperty.IsModified && stockProperty.OriginalValue != stockProperty.CurrentValue;
        bool costChanged = costProperty.IsModified && costProperty.OriginalValue != costProperty.CurrentValue;

        if (stockChanged)
        {
            int delta = stockProperty.CurrentValue - stockProperty.OriginalValue;
            StockMovementType movementType = delta >= 0 ? StockMovementType.Increase : StockMovementType.Decrease;

            // When cost changed in the same save, the movement carries the new cost, so the
            // point-in-time valuation stays correct without a separate CostAdjustment row.
            return StockMovement.Record(
                product.TenantId,
                product.Id,
                movementType,
                delta,
                stockProperty.CurrentValue,
                product.Cost,
                utcNow,
                userId);
        }

        if (costChanged)
        {
            return StockMovement.Record(
                product.TenantId,
                product.Id,
                StockMovementType.CostAdjustment,
                0,
                product.StockQuantity,
                product.Cost,
                utcNow,
                userId);
        }

        return null;
    }

    private string? ResolveUserId()
    {
        try
        {
            return tenantContext?.UserId;
        }
        catch
        {
            return null;
        }
    }
}
