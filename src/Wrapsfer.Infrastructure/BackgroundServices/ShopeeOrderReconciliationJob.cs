using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class ShopeeOrderReconciliationJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeOrderReconciliationJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ShopeeOrderSyncOptions syncOptions = options.Value.OrderSync;
        if (!syncOptions.Enabled)
        {
            logger.LogInformation("Shopee order reconciliation job is disabled by configuration");
            return;
        }

        int intervalMinutes = Math.Max(syncOptions.ReconciliationIntervalMinutes, 1);
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(intervalMinutes));

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
            ShopeeOrderReconciliationProcessor processor =
                scope.ServiceProvider.GetRequiredService<ShopeeOrderReconciliationProcessor>();

            await processor.RunAsync(
                syncOptions.WindowHours, syncOptions.TrackingRetryThresholdMinutes, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown mid-run
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShopeeOrderReconciliationJob iteration failed");
        }
    }
}
