namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeShopItemResponse(
    long ItemId,
    string ItemName,
    string? ItemSku,
    string ItemStatus,
    bool HasModel,
    int? StockQuantity,
    string? ImageUrl,
    ShopeeProductLinkResponse? Link,
    IReadOnlyList<ShopeeShopItemModelResponse> Models);
