namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeOrderDetail(string OrderSn, string Status, string? Region, string? BuyerUsername,
    string? RecipientName, string? RecipientPhone, string? RecipientAddress, decimal TotalAmount,
    string? Currency, decimal? CodAmount, string? ShippingCarrier, DateTime? ShipByDate,
    IReadOnlyList<ShopeeOrderDetailItem> Items);
