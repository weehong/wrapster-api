using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Products.Services;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class LowStockReminderJob(
    IServiceScopeFactory scopeFactory,
    ILogger<LowStockReminderJob> logger) : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromHours(1);

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
            LowStockReminderProcessor processor =
                scope.ServiceProvider.GetRequiredService<LowStockReminderProcessor>();

            LowStockReminderRunSummary summary = await processor.RunAsync(cancellationToken);

            if (summary.SentCount > 0 || summary.SkippedCount > 0)
            {
                logger.LogInformation(
                    "LowStockReminderJob completed: sent {Sent}, skipped {Skipped}",
                    summary.SentCount, summary.SkippedCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LowStockReminderJob iteration failed");
        }
    }
}
