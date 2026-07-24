using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class ShopeeAutoArrangeJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeAutoArrangeJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ShopeeAutoArrangeOptions autoArrangeOptions = options.Value.AutoArrange;
        if (!autoArrangeOptions.Enabled)
        {
            logger.LogInformation("Shopee auto-arrange job is disabled by configuration");
            return;
        }

        int intervalMinutes = Math.Max(autoArrangeOptions.IntervalMinutes, 1);
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(intervalMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteOnceAsync(autoArrangeOptions, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ExecuteOnceAsync(
        ShopeeAutoArrangeOptions autoArrangeOptions, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ShopeeAutoArrangeProcessor processor =
                scope.ServiceProvider.GetRequiredService<ShopeeAutoArrangeProcessor>();

            await processor.RunAsync(autoArrangeOptions.OrderBatchSize, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown mid-run
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShopeeAutoArrangeJob iteration failed");
        }
    }
}
