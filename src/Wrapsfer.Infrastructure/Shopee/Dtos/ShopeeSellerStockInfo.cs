using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeSellerStockInfo
{
    [JsonPropertyName("location_id")]
    public string? LocationId { get; set; }

    [JsonPropertyName("stock")]
    public int? Stock { get; set; }
}
