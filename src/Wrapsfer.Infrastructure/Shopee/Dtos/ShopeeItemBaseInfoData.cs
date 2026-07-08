using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeItemBaseInfoData
{
    [JsonPropertyName("item_list")]
    public List<ShopeeItemBaseInfoItem>? ItemList { get; set; }
}
