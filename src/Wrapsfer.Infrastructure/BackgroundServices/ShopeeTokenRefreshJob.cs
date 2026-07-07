using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Shopee.Services;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class ShopeeTokenRefreshJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ShopeeTokenRefreshJob> logger) : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(s_interval);

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
            ShopeeTokenRefreshProcessor processor =
                scope.ServiceProvider.GetRequiredService<ShopeeTokenRefreshProcessor>();

            ShopeeTokenRefreshRunSummary summary = await processor.RunAsync(cancellationToken);

            if (summary.RefreshedCount > 0 || summary.FailedCount > 0 || summary.ExpiredCount > 0)
            {
                logger.LogInformation(
                    "ShopeeTokenRefreshJob completed: refreshed {Refreshed}, failed {Failed}, expired {Expired}",
                    summary.RefreshedCount, summary.FailedCount, summary.ExpiredCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown mid-run
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShopeeTokenRefreshJob iteration failed");
        }
    }
}
