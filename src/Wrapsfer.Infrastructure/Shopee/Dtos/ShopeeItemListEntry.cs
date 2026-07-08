using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeItemListEntry
{
    [JsonPropertyName("item_id")]
    public long ItemId { get; set; }
}
