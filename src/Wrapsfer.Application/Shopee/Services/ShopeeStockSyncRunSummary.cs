namespace Wrapsfer.Application.Shopee.Services;

public sealed record ShopeeStockSyncRunSummary(
    int SyncedCount,
    int FailedCount,
    int SkippedCount);
