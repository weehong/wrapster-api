using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeTrackingNumberData
{
    [JsonPropertyName("tracking_number")]
    public string? TrackingNumber { get; set; }
}
