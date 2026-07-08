namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeItemModel(
    long ModelId,
    string ModelName,
    string? ModelSku,
    int? StockQuantity);
