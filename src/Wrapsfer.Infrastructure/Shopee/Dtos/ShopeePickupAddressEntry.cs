using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeePickupAddressEntry
{
    [JsonPropertyName("address_id")]
    public long AddressId { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("time_slot_list")]
    public List<ShopeePickupTimeSlotEntry>? TimeSlotList { get; set; }
}
