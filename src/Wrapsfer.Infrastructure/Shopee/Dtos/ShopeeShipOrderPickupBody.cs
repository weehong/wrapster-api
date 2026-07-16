using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed record ShopeeShipOrderPickupBody(
    [property: JsonPropertyName("address_id")] long AddressId,
    [property: JsonPropertyName("pickup_time_id")] string PickupTimeId);
