using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class ShopeeWebhookDispatchJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeWebhookDispatchJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ShopeeOrderSyncOptions syncOptions = options.Value.OrderSync;
        if (!syncOptions.Enabled)
        {
            logger.LogInformation("Shopee webhook dispatch job is disabled by configuration");
            return;
        }

        int intervalSeconds = Math.Max(syncOptions.WebhookDispatchIntervalSeconds, 1);
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteOnceAsync(syncOptions, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ExecuteOnceAsync(ShopeeOrderSyncOptions syncOptions, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ShopeeWebhookEventProcessor processor =
                scope.ServiceProvider.GetRequiredService<ShopeeWebhookEventProcessor>();

            ShopeeWebhookRunSummary summary = await processor.RunAsync(
                syncOptions.WebhookBatchSize, syncOptions.WebhookMaxAttempts, cancellationToken);
            if (summary.ProcessedCount + summary.FailedCount + summary.IgnoredCount > 0)
            {
                logger.LogInformation(
                    "ShopeeWebhookDispatchJob completed: processed {Processed}, failed {Failed}, ignored {Ignored}",
                    summary.ProcessedCount, summary.FailedCount, summary.IgnoredCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown mid-run
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShopeeWebhookDispatchJob iteration failed");
        }
    }
}
