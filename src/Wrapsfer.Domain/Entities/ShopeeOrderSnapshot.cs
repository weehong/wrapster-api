namespace Wrapsfer.Domain.Entities;

/// <summary>Scalar fields copied from Shopee's get_order_detail on every sync.</summary>
public sealed record ShopeeOrderSnapshot(
    string ShopeeStatus,
    string? BuyerUsername,
    string? RecipientName,
    string? RecipientPhone,
    string? RecipientAddress,
    decimal TotalAmount,
    string? Currency,
    decimal? CodAmount,
    string? ShippingCarrier,
    DateTime? ShipByDate);
