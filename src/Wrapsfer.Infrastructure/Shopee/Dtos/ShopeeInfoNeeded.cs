using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeInfoNeeded
{
    [JsonPropertyName("pickup")]
    public List<string>? Pickup { get; set; }

    [JsonPropertyName("dropoff")]
    public List<string>? Dropoff { get; set; }
}
