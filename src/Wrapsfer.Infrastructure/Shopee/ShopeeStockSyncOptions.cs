namespace Wrapsfer.Infrastructure.Shopee;

public sealed class ShopeeStockSyncOptions
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 5;
    public int BatchSize { get; set; } = 200;
}
