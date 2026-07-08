using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeUpdateStockRequest
{
    public ShopeeUpdateStockRequest(long itemId, IReadOnlyList<ShopeeUpdateStockStockList> stockList)
    {
        ItemId = itemId;
        StockList = stockList;
    }

    [JsonPropertyName("item_id")]
    public long ItemId { get; }

    [JsonPropertyName("stock_list")]
    public IReadOnlyList<ShopeeUpdateStockStockList> StockList { get; }
}
