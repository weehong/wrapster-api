namespace Wrapsfer.Application.Shopee.Services;

public sealed record ShopeeWebhookRunSummary(int ProcessedCount, int FailedCount, int IgnoredCount);
