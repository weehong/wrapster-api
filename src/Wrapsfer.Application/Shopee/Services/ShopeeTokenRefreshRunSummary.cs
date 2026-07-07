namespace Wrapsfer.Application.Shopee.Services;

public sealed record ShopeeTokenRefreshRunSummary(int RefreshedCount, int FailedCount, int ExpiredCount);
