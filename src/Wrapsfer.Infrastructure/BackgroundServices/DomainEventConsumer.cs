using System.Reflection;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Infrastructure.Queue;
using Wrapsfer.Infrastructure.Queue.Messaging;

namespace Wrapsfer.Infrastructure.BackgroundServices;

/// <summary>
/// Consumes domain events from the broker and dispatches them in-process to their
/// MediatR <see cref="IDomainEventHandler{TDomainEvent}"/> handlers, off the request
/// thread. This is the delivery half of the transactional outbox: the originating
/// request has long since returned, so a handler failure here only affects the
/// notification, never the business operation.
/// </summary>
public sealed class DomainEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<DomainEventConsumer> logger) : BackgroundService
{
    private static readonly Assembly s_domainAssembly = typeof(IDomainEvent).Assembly;
    private readonly RabbitMqOptions _rabbitOptions = rabbitOptions.Value;
    private IChannel? _channel;
    private IConnection? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            ConnectionFactory factory = new()
            {
                HostName = _rabbitOptions.HostName,
                Port = _rabbitOptions.Port,
                UserName = _rabbitOptions.UserName,
                Password = _rabbitOptions.Password,
                VirtualHost = _rabbitOptions.VirtualHost
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                DomainEventMessage.QueueName,
                true,
                false,
                false,
                cancellationToken: stoppingToken);

            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            AsyncEventingBasicConsumer consumer = new(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    string json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    DomainEventMessage? message = JsonSerializer.Deserialize<DomainEventMessage>(json);

                    if (message is null)
                    {
                        logger.LogWarning("Received empty or malformed domain event message");
                    }
                    else
                    {
                        await HandleAsync(message, stoppingToken);
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to process domain event message — dropping (requeue disabled to avoid infinite retries)");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                DomainEventMessage.QueueName,
                false,
                consumer,
                stoppingToken);

            logger.LogInformation("Domain event consumer started; listening on queue {Queue}",
                DomainEventMessage.QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Domain event consumer terminated unexpectedly");
        }
    }

    private async Task HandleAsync(DomainEventMessage message, CancellationToken cancellationToken)
    {
        Type? eventType = s_domainAssembly.GetType(message.Type);
        if (eventType is null)
        {
            logger.LogWarning(
                "Unknown domain event type '{Type}' — skipping (no handler can be resolved)", message.Type);
            return;
        }

        object? domainEvent = JsonSerializer.Deserialize(message.Content, eventType);
        if (domainEvent is null)
        {
            logger.LogWarning("Failed to deserialize domain event '{Type}' — skipping", message.Type);
            return;
        }

        Type notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
        if (Activator.CreateInstance(notificationType, domainEvent) is not INotification notification)
        {
            throw new InvalidOperationException(
                $"Failed to create notification for domain event type '{message.Type}'.");
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        IPublisher publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(notification, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
            _connection.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
