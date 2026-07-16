namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeShipOrderRequest(
    string OrderSn, ShopeeShipOrderPickup? Pickup, ShopeeShipOrderDropoff? Dropoff);
