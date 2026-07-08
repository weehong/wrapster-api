using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class ShopeeStockSyncJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeStockSyncJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ShopeeStockSyncOptions stockSyncOptions = options.Value.StockSync;
        if (!stockSyncOptions.Enabled)
        {
            logger.LogInformation("Shopee stock sync job is disabled by configuration");
            return;
        }

        int intervalMinutes = Math.Max(stockSyncOptions.IntervalMinutes, 1);
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(intervalMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ShopeeStockSyncProcessor processor =
                scope.ServiceProvider.GetRequiredService<ShopeeStockSyncProcessor>();

            ShopeeStockSyncRunSummary summary = await processor.RunAsync(cancellationToken);
            logger.LogInformation(
                "ShopeeStockSyncJob completed: synced {Synced}, failed {Failed}, skipped {Skipped}",
                summary.SyncedCount, summary.FailedCount, summary.SkippedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown mid-run
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShopeeStockSyncJob iteration failed");
        }
    }
}
