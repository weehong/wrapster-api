using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeStockSummaryInfo
{
    [JsonPropertyName("total_available_stock")]
    public int? TotalAvailableStock { get; set; }
}
