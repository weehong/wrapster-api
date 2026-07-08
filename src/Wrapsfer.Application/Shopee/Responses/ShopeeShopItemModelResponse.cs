namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeShopItemModelResponse(
    long ModelId,
    string ModelName,
    string? ModelSku,
    int? StockQuantity,
    ShopeeProductLinkResponse? Link);
