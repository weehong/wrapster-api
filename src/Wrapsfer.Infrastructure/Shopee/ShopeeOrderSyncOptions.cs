namespace Wrapsfer.Infrastructure.Shopee;

public sealed class ShopeeOrderSyncOptions
{
    public bool Enabled { get; set; }
    public int ReconciliationIntervalMinutes { get; set; } = 60;
    public int WindowHours { get; set; } = 24;
    public int TrackingRetryThresholdMinutes { get; set; } = 30;
    public int WebhookDispatchIntervalSeconds { get; set; } = 10;
    public int WebhookMaxAttempts { get; set; } = 5;
    public int WebhookBatchSize { get; set; } = 50;
}
