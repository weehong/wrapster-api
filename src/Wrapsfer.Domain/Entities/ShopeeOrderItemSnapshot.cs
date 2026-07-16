namespace Wrapsfer.Domain.Entities;

/// <summary>
/// One Shopee order line plus its resolution against ShopeeProductLinks.
/// A null <see cref="ProductId"/> means the Shopee item/model is not linked
/// to a Wrapsfer product, which blocks shipment (NeedsLinking).
/// </summary>
public sealed record ShopeeOrderItemSnapshot(
    long ShopeeItemId,
    long ShopeeModelId,
    string? ItemName,
    string? ModelName,
    string? ItemSku,
    int Quantity,
    Guid? ProductId);
