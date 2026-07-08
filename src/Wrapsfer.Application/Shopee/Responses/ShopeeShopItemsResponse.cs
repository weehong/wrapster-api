namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeShopItemsResponse(
    IReadOnlyList<ShopeeShopItemResponse> Items,
    bool HasNextPage,
    int NextOffset,
    int TotalCount);
