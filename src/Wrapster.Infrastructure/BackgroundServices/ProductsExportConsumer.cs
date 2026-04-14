using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Products.Messaging;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Repositories;
using Wrapster.Infrastructure.FileProcessing;
using Wrapster.Infrastructure.Queue;
using Wrapster.Mailing.Abstractions;
using TenantSettingsEntity = Wrapster.Domain.Entities.TenantSettings;

namespace Wrapster.Infrastructure.BackgroundServices;

public sealed class ProductsExportConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<ProductsExportConsumer> logger) : BackgroundService
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
                ProductsExportRequestedMessage.QueueName,
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
                    ProductsExportRequestedMessage? message =
                        JsonSerializer.Deserialize<ProductsExportRequestedMessage>(json);

                    if (message is null)
                    {
                        logger.LogWarning("Received empty or malformed products export message");
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
                        "Failed to process products export message — dropping (requeue disabled to avoid infinite retries)");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                ProductsExportRequestedMessage.QueueName,
                false,
                consumer,
                stoppingToken);

            logger.LogInformation("Products export consumer started; listening on queue {Queue}",
                ProductsExportRequestedMessage.QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Products export consumer terminated unexpectedly");
        }
    }

    private async Task HandleAsync(ProductsExportRequestedMessage message, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();

        IProductRepository productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        IProductComponentRepository componentRepository =
            scope.ServiceProvider.GetRequiredService<IProductComponentRepository>();
        IProductFileWriter writer = scope.ServiceProvider.GetRequiredService<IProductFileWriter>();
        IMailer mailer = scope.ServiceProvider.GetRequiredService<IMailer>();
        ITenantSettingsRepository tenantSettingsRepository =
            scope.ServiceProvider.GetRequiredService<ITenantSettingsRepository>();

        List<Product> allProducts = [];
        int page = 1;
        int totalCount;
        do
        {
            (IReadOnlyList<Product> items, int total) = await productRepository.ListAsync(
                message.TenantId, null, null, page, ExportPageSize,
                cancellationToken);
            allProducts.AddRange(items);
            totalCount = total;
            page++;
        } while (allProducts.Count < totalCount);

        Dictionary<Guid, string> barcodeById = allProducts.ToDictionary(p => p.Id, p => p.Barcode);

        List<Guid> bundleIds = allProducts.Where(p => p.Type == ProductType.Bundle).Select(p => p.Id).ToList();
        IReadOnlyList<ProductComponent> allComponents = bundleIds.Count > 0
            ? await componentRepository.GetByParentIdsAsync(bundleIds, message.TenantId, cancellationToken)
            : [];

        ILookup<Guid, (Guid ChildId, int Quantity)> componentsByParent = allComponents
            .ToLookup(c => c.ParentProductId, c => (c.ChildProductId, c.Quantity));

        List<ProductExportRow> rows = allProducts
            .Select(p => ProductExportRowMapper.ToRow(
                p,
                barcodeById,
                p.Type == ProductType.Bundle ? componentsByParent[p.Id].ToList() : null))
            .ToList();

        string contentType = message.Format == ProductFileFormat.Csv
            ? "text/csv"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        byte[] fileBytes = await writer.WriteAsync(rows, message.Format, cancellationToken);

        TenantSettingsEntity? tenantSettings =
            await tenantSettingsRepository.GetByTenantIdAsync(message.TenantId, cancellationToken);

        List<string> recipients = tenantSettings?.Recipients
            .Where(r => r.IsActive)
            .Select(r => r.Email)
            .ToList() ?? [];

        if (recipients.Count == 0 && !string.IsNullOrWhiteSpace(message.RequestedBy) &&
            message.RequestedBy.Contains('@'))
        {
            recipients.Add(message.RequestedBy);
        }

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Products export produced for tenant {TenantId} but no recipients were configured",
                message.TenantId);
            return;
        }

        string extension = message.Format == ProductFileFormat.Csv ? ".csv" : ".xlsx";
        string fileName = $"products-export-{message.RequestedAt:yyyyMMdd-HHmmss}{extension}";

        MailMessage mail = new()
        {
            To = recipients,
            TemplateName = "products-export",
            Tokens = new Dictionary<string, object?>
            {
                ["ProductCount"] = rows.Count,
                ["RequestedAt"] = message.RequestedAt.ToString("yyyy-MM-dd"),
                ["RequestedAtUtc"] = message.RequestedAt.ToString("yyyy-MM-dd HH:mm")
            },
            Attachments = [new MailAttachment(fileName, fileBytes, contentType)]
        };

        MailRequestId requestId = await mailer.SendAsync(mail, cancellationToken);

        logger.LogInformation(
            "Enqueued products export mail {MailRequestId} for tenant {TenantId} ({Count} rows, format {Format}) to {RecipientCount} recipient(s)",
            requestId, message.TenantId, rows.Count, message.Format, recipients.Count);
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
