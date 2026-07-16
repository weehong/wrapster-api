using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeGetOrderListData
{
    [JsonPropertyName("order_list")]
    public List<ShopeeOrderListEntry>? OrderList { get; set; }

    [JsonPropertyName("more")]
    public bool More { get; set; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}
