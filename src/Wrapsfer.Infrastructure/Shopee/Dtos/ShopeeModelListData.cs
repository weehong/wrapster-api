using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeModelListData
{
    [JsonPropertyName("model")]
    public List<ShopeeModelInfo>? Model { get; set; }
}
