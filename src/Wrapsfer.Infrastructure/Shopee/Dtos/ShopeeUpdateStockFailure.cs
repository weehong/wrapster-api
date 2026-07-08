using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeUpdateStockFailure
{
    [JsonPropertyName("model_id")]
    public long ModelId { get; set; }

    [JsonPropertyName("failed_reason")]
    public string? FailedReason { get; set; }
}
