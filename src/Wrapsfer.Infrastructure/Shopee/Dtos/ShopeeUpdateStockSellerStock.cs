using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeUpdateStockSellerStock
{
    public ShopeeUpdateStockSellerStock(int stock)
    {
        Stock = stock;
    }

    [JsonPropertyName("stock")]
    public int Stock { get; }
}
