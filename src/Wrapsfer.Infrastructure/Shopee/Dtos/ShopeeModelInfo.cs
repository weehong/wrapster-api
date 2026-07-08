using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeModelInfo
{
    [JsonPropertyName("model_id")]
    public long ModelId { get; set; }

    [JsonPropertyName("model_name")]
    public string? ModelName { get; set; }

    [JsonPropertyName("model_sku")]
    public string? ModelSku { get; set; }

    [JsonPropertyName("stock_info_v2")]
    public ShopeeStockInfoV2? StockInfoV2 { get; set; }
}
