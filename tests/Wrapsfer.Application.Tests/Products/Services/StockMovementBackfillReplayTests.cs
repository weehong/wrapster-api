using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Infrastructure.BackgroundServices;

namespace Wrapsfer.Application.Tests.Products.Services;

public class StockMovementBackfillReplayTests
{
    private static Product NewProduct() =>
        Product.Create("t1", "BC", "Alpha", ProductType.Single, cost: 6.50m, stockQuantity: 0).Value;

    private static AuditLog Audit(AuditAction action, string changes, DateTime timestamp) =>
        new()
        {
            EntityName = nameof(Product),
            EntityId = Guid.NewGuid().ToString(),
            Action = action,
            Changes = changes,
            UserId = "u",
            Timestamp = timestamp
        };

    [Fact]
    public void ReplayProduct_ReconstructsMovementsWithCarriedForwardCost()
    {
        Product product = NewProduct();
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        List<AuditLog> audits =
        [
            Audit(AuditAction.Created,
                """{"StockQuantity":{"NewValue":10},"Cost":{"NewValue":5.00},"Name":{"NewValue":"Alpha"}}""",
                t0),
            Audit(AuditAction.Updated, """{"StockQuantity":{"OldValue":10,"NewValue":7}}""", t0.AddHours(1)),
            Audit(AuditAction.Updated, """{"Cost":{"OldValue":5.00,"NewValue":6.50}}""", t0.AddHours(2)),
            Audit(AuditAction.Updated, """{"StockQuantity":{"OldValue":7,"NewValue":12}}""", t0.AddHours(3)),
            Audit(AuditAction.Updated, """{"Name":{"OldValue":"Alpha","NewValue":"Alpha2"}}""", t0.AddHours(4))
        ];

        List<StockMovement> movements = StockMovementBackfillJob.ReplayProduct(product, audits);

        // The name-only change produces no ledger row.
        movements.Should().HaveCount(4);

        movements.Select(m => m.MovementType).Should().Equal(
            StockMovementType.Created,
            StockMovementType.Decrease,
            StockMovementType.CostAdjustment,
            StockMovementType.Increase);

        movements.Select(m => m.QuantityAfter).Should().Equal(10, 7, 7, 12);
        movements.Select(m => m.QuantityDelta).Should().Equal(10, -3, 0, 5);

        // Cost is carried forward: 5.00 until the cost change at t+2, then 6.50.
        movements.Select(m => m.UnitCost).Should().Equal(5.00m, 5.00m, 6.50m, 6.50m);
        movements.Should().OnlyContain(m => m.TenantId == "t1" && m.ProductId == product.Id);
    }

    [Fact]
    public void ReplayProduct_WithNoStockOrCostHistory_ReturnsEmpty()
    {
        Product product = NewProduct();
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        List<AuditLog> audits =
        [
            Audit(AuditAction.Updated, """{"Name":{"OldValue":"Alpha","NewValue":"Beta"}}""", t0)
        ];

        List<StockMovement> movements = StockMovementBackfillJob.ReplayProduct(product, audits);

        movements.Should().BeEmpty();
    }
}
