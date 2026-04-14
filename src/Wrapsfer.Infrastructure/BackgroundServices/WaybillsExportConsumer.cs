using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Infrastructure.Queue;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class WaybillsExportConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<WaybillsExportConsumer> logger) : BackgroundService
{
    private const int ExportPageSize = 500;
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
                WaybillsExportRequestedMessage.QueueName,
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
                    WaybillsExportRequestedMessage? message =
                        JsonSerializer.Deserialize<WaybillsExportRequestedMessage>(json);

                    if (message is null)
                    {
                        logger.LogWarning("Received empty or malformed waybills export message");
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
                        "Failed to process waybills export message — dropping (requeue disabled)");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                WaybillsExportRequestedMessage.QueueName,
                false,
                consumer,
                stoppingToken);

            logger.LogInformation("Waybills export consumer started; listening on queue {Queue}",
                WaybillsExportRequestedMessage.QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Waybills export consumer terminated unexpectedly");
        }
    }

    private async Task HandleAsync(WaybillsExportRequestedMessage message, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();

        IWaybillRepository waybillRepository = scope.ServiceProvider.GetRequiredService<IWaybillRepository>();

        List<Waybill> all = [];
        int page = 1;
        int totalCount;
        do
        {
            (IReadOnlyList<Waybill> items, int total) = await waybillRepository.ListAsync(
                message.TenantId, null, null, null, null,
                page, ExportPageSize, cancellationToken);
            all.AddRange(items);
            totalCount = total;
            page++;
        } while (all.Count < totalCount);

        logger.LogInformation(
            "Waybills export prepared for tenant {TenantId}: {Count} waybills in {Format}. " +
            "Delivery pipeline (file generation + email) is a future iteration.",
            message.TenantId, all.Count, message.Format);
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
