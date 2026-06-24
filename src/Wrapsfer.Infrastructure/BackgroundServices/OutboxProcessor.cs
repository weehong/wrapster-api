using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Infrastructure.Persistence;
using Wrapsfer.Infrastructure.Persistence.Outbox;
using Wrapsfer.Infrastructure.Queue.Messaging;

namespace Wrapsfer.Infrastructure.BackgroundServices;

/// <summary>
/// Relays durably-stored domain events to the message broker. Polls unprocessed
/// <see cref="OutboxMessage"/> rows and publishes each to the domain-events queue,
/// stamping <see cref="OutboxMessage.ProcessedOnUtc"/> on success. A publish failure
/// leaves the row unprocessed (with an incremented retry count) so it is retried on the
/// next poll rather than lost — this is the durability half of the transactional outbox.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IMessagePublisher messagePublisher,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxRetries = 10;
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started; polling every {Interval}s", s_pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox processor batch failed; retrying after delay");
            }

            try
            {
                await Task.Delay(s_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<OutboxMessage> messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (OutboxMessage message in messages)
        {
            try
            {
                DomainEventMessage envelope = new(
                    message.Id,
                    message.Type,
                    message.Content,
                    message.TenantId,
                    message.OccurredOnUtc);

                await messagePublisher.PublishAsync(DomainEventMessage.QueueName, envelope, cancellationToken);

                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                logger.LogError(ex,
                    "Failed to publish outbox message {OutboxId} ({Type}) — attempt {RetryCount}/{MaxRetries}",
                    message.Id, message.Type, message.RetryCount, MaxRetries);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
