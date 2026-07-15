namespace Wrapsfer.Api.Contracts;

public sealed record CreateShopeeLinkedProductRequest(
    long ShopeeItemId,
    long ShopeeModelId,
    string Barcode,
    string Name,
    string? SkuCode,
    decimal Cost,
    int StockQuantity,
    int? LowStockThreshold);
