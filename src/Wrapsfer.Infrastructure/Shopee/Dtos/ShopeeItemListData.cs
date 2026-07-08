using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeItemListData
{
    [JsonPropertyName("item")]
    public List<ShopeeItemListEntry>? Item { get; set; }

    [JsonPropertyName("has_next_page")]
    public bool HasNextPage { get; set; }

    [JsonPropertyName("next_offset")]
    public int NextOffset { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}
