using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeShippingParameterData
{
    [JsonPropertyName("info_needed")]
    public ShopeeInfoNeeded? InfoNeeded { get; set; }

    [JsonPropertyName("pickup")]
    public ShopeePickupData? Pickup { get; set; }

    [JsonPropertyName("dropoff")]
    public ShopeeDropoffData? Dropoff { get; set; }
}
