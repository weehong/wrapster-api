using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeOrderDetailEntry
{
    [JsonPropertyName("order_sn")]
    public string OrderSn { get; set; } = string.Empty;

    [JsonPropertyName("order_status")]
    public string? OrderStatus { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("buyer_username")]
    public string? BuyerUsername { get; set; }

    [JsonPropertyName("recipient_address")]
    public ShopeeRecipientAddress? RecipientAddress { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("cod")]
    public bool Cod { get; set; }

    [JsonPropertyName("shipping_carrier")]
    public string? ShippingCarrier { get; set; }

    [JsonPropertyName("ship_by_date")]
    public long? ShipByDate { get; set; }

    [JsonPropertyName("item_list")]
    public List<ShopeeOrderItemEntry>? ItemList { get; set; }
}
