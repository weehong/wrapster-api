using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed record ShopeeGetTokenRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("shop_id")] long ShopId,
    [property: JsonPropertyName("partner_id")] long PartnerId);
