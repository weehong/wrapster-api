using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeUpdateStockResult
{
    [JsonPropertyName("failure_list")]
    public List<ShopeeUpdateStockFailure>? FailureList { get; set; }
}
