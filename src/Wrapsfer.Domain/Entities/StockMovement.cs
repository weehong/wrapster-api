using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Entities;

/// <summary>
/// Append-only inventory ledger row. One row is recorded whenever a product's
/// <see cref="Product.StockQuantity"/> or <see cref="Product.Cost"/> changes, capturing the
/// resulting balance and unit cost so inventory and valuation can be reconstructed as of any date.
/// </summary>
public sealed class StockMovement
{
    private StockMovement()
    {
    }

    public Guid Id { get; private init; } = Guid.NewGuid();
    public string TenantId { get; private init; } = default!;
    public Guid ProductId { get; private init; }
    public StockMovementType MovementType { get; private init; }
    public int QuantityDelta { get; private init; }
    public int QuantityAfter { get; private init; }
    public decimal UnitCost { get; private init; }
    public DateTime OccurredAt { get; private init; }
    public string? UserId { get; private init; }

    public static StockMovement Record(
        string tenantId,
        Guid productId,
        StockMovementType movementType,
        int quantityDelta,
        int quantityAfter,
        decimal unitCost,
        DateTime occurredAt,
        string? userId) =>
        new()
        {
            TenantId = tenantId,
            ProductId = productId,
            MovementType = movementType,
            QuantityDelta = quantityDelta,
            QuantityAfter = quantityAfter,
            UnitCost = unitCost,
            OccurredAt = occurredAt,
            UserId = userId
        };
}
