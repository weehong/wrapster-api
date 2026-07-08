using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeStockInfoV2
{
    [JsonPropertyName("summary_info")]
    public ShopeeStockSummaryInfo? SummaryInfo { get; set; }

    [JsonPropertyName("seller_stock")]
    public List<ShopeeSellerStockInfo>? SellerStock { get; set; }
}
