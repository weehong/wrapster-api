using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

/// <summary>Generic Shopee response envelope for endpoints whose <c>response</c> payload is not consumed.</summary>
internal sealed class ShopeeEnvelopeResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
