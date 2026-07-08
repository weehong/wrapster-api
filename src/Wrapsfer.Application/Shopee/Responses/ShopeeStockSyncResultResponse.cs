namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeStockSyncResultResponse(
    int SyncedCount,
    int FailedCount,
    int SkippedCount);
