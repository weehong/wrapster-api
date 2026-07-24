namespace Wrapsfer.Infrastructure.Shopee;

public sealed class ShopeeAutoArrangeOptions
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 5;
    public int OrderBatchSize { get; set; } = 100;
}
