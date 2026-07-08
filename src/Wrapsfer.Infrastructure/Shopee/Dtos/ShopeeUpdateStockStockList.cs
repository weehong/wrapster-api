using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeUpdateStockStockList
{
    public ShopeeUpdateStockStockList(
        long modelId,
        IReadOnlyList<ShopeeUpdateStockSellerStock> sellerStock)
    {
        ModelId = modelId;
        SellerStock = sellerStock;
    }

    [JsonPropertyName("model_id")]
    public long ModelId { get; }

    [JsonPropertyName("seller_stock")]
    public IReadOnlyList<ShopeeUpdateStockSellerStock> SellerStock { get; }
}
