namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeOrderItemResponse(
    Guid Id,
    long ShopeeItemId,
    long ShopeeModelId,
    string? ItemName,
    string? ModelName,
    string? ItemSku,
    int Quantity,
    Guid? ProductId);
