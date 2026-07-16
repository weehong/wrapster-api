using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed record ShopeeShippingDocumentOrderRequest(
    [property: JsonPropertyName("order_list")] IReadOnlyList<ShopeeShippingDocumentOrderEntry> OrderList);
