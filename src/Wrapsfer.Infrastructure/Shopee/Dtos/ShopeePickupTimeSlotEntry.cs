using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeePickupTimeSlotEntry
{
    [JsonPropertyName("pickup_time_id")]
    public string? PickupTimeId { get; set; }

    [JsonPropertyName("date")]
    public long Date { get; set; }

    [JsonPropertyName("time_text")]
    public string? TimeText { get; set; }
}
