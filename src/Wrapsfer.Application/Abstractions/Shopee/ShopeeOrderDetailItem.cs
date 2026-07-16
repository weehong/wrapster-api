namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeOrderDetailItem(
    long ItemId, long ModelId, string? ItemName, string? ModelName, string? ItemSku, int Quantity);
