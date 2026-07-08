namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeItemDetail(
    long ItemId,
    string ItemName,
    string? ItemSku,
    string ItemStatus,
    bool HasModel,
    int? StockQuantity,
    string? ImageUrl);
