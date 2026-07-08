namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeProductLinkResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductBarcode,
    int ProductAvailableQuantity,
    long ShopeeItemId,
    long ShopeeModelId,
    string? ShopeeItemName,
    string? ShopeeModelName,
    string? ShopeeItemSku,
    int? LastSyncedQuantity,
    DateTime? LastSyncedAt,
    string? LastSyncError);
