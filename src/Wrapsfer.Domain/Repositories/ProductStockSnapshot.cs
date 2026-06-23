namespace Wrapsfer.Domain.Repositories;

/// <summary>
/// Point-in-time stock and cost for a single product: the resulting balance and unit cost
/// from that product's most recent <see cref="Entities.StockMovement"/> at or before a given instant.
/// </summary>
public sealed record ProductStockSnapshot(
    Guid ProductId,
    string Barcode,
    string? SkuCode,
    string Name,
    int Quantity,
    decimal UnitCost);
