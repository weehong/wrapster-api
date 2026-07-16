using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeOrderListEntry
{
    [JsonPropertyName("order_sn")]
    public string? OrderSn { get; set; }
}
