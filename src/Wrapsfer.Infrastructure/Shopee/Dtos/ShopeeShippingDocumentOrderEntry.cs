using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed record ShopeeShippingDocumentOrderEntry(
    [property: JsonPropertyName("order_sn")] string OrderSn);
