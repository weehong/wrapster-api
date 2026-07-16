using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed record ShopeeShipOrderApiRequest(
    [property: JsonPropertyName("order_sn")] string OrderSn,
    [property: JsonPropertyName("pickup"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ShopeeShipOrderPickupBody? Pickup,
    [property: JsonPropertyName("dropoff"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ShopeeShipOrderDropoffBody? Dropoff);
