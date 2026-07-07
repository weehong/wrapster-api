using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed record ShopeeRefreshTokenRequest(
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("shop_id")] long ShopId,
    [property: JsonPropertyName("partner_id")] long PartnerId);
