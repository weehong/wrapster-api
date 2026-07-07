using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeShopInfoResponse
{
    [JsonPropertyName("shop_name")]
    public string? ShopName { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
