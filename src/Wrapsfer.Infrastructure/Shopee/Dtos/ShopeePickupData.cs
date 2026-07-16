using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeePickupData
{
    [JsonPropertyName("address_list")]
    public List<ShopeePickupAddressEntry>? AddressList { get; set; }
}
