using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeItemBaseInfoItem
{
    [JsonPropertyName("item_id")]
    public long ItemId { get; set; }

    [JsonPropertyName("item_name")]
    public string? ItemName { get; set; }

    [JsonPropertyName("item_sku")]
    public string? ItemSku { get; set; }

    [JsonPropertyName("item_status")]
    public string? ItemStatus { get; set; }

    [JsonPropertyName("has_model")]
    public bool HasModel { get; set; }

    [JsonPropertyName("stock_info_v2")]
    public ShopeeStockInfoV2? StockInfoV2 { get; set; }

    [JsonPropertyName("image")]
    public ShopeeImageInfo? Image { get; set; }

    [JsonPropertyName("image_info")]
    public ShopeeImageInfo? ImageInfo { get; set; }
}
