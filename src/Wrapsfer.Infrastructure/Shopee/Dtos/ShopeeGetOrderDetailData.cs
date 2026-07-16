using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeGetOrderDetailData
{
    [JsonPropertyName("order_list")]
    public List<ShopeeOrderDetailEntry>? OrderList { get; set; }
}
