using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Products.EventHandlers;

internal sealed class LowStockDetectedEventHandler(
    IMailer mailer,
    ITenantSettingsRepository tenantSettingsRepository,
    IStockAlertLogRepository stockAlertLogRepository,
    IUnitOfWork unitOfWork,
    ILogger<LowStockDetectedEventHandler> logger)
    : IDomainEventHandler<LowStockDetectedEvent>
{
    private static readonly TimeSpan s_dedupeWindow = TimeSpan.FromHours(24);

    public async Task Handle(DomainEventNotification<LowStockDetectedEvent> notification,
        CancellationToken cancellationToken)
    {
        LowStockDetectedEvent domainEvent = notification.DomainEvent;

        StockAlertLog? lastSent = await stockAlertLogRepository.GetLastAlertAsync(
            domainEvent.TenantId,
            domainEvent.ProductId,
            StockAlertType.LowStock,
            StockAlertDeliveryStatus.Sent,
            cancellationToken);

        if (lastSent is not null && DateTime.UtcNow - lastSent.OccurredOn < s_dedupeWindow)
        {
            logger.LogInformation(
                "Suppressing low-stock alert for tenant {TenantId} product {ProductId} — last sent {OccurredOn}",
                domainEvent.TenantId, domainEvent.ProductId, lastSent.OccurredOn);

            stockAlertLogRepository.Add(StockAlertLog.Create(
                domainEvent.TenantId,
                domainEvent.ProductId,
                domainEvent.ProductName,
                domainEvent.Barcode,
                StockAlertType.LowStock,
                domainEvent.CurrentStock,
                domainEvent.Threshold,
                StockAlertDeliveryStatus.Suppressed,
                recipientsNotified: null,
                failureReason: "DedupeWindow",
                occurredOn: DateTime.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(domainEvent.TenantId, cancellationToken);

        List<string> recipients = settings?.Recipients
            .Where(r => r.IsActive)
            .Select(r => r.Email)
            .ToList() ?? [];

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "No active recipients configured for tenant {TenantId} — skipping low stock alert for product {ProductName} ({Barcode})",
                domainEvent.TenantId, domainEvent.ProductName, domainEvent.Barcode);

            stockAlertLogRepository.Add(StockAlertLog.Create(
                domainEvent.TenantId,
                domainEvent.ProductId,
                domainEvent.ProductName,
                domainEvent.Barcode,
                StockAlertType.LowStock,
                domainEvent.CurrentStock,
                domainEvent.Threshold,
                StockAlertDeliveryStatus.Failed,
                recipientsNotified: null,
                failureReason: "NoRecipients",
                occurredOn: DateTime.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        MailMessage mail = new()
        {
            To = recipients,
            TemplateName = "low-stock-alert",
            Tokens = new Dictionary<string, object?>
            {
                ["ProductName"] = domainEvent.ProductName,
                ["Barcode"] = domainEvent.Barcode,
                ["CurrentStock"] = domainEvent.CurrentStock,
                ["Threshold"] = domainEvent.Threshold
            }
        };

        MailRequestId requestId = await mailer.SendAsync(mail, cancellationToken);

        stockAlertLogRepository.Add(StockAlertLog.Create(
            domainEvent.TenantId,
            domainEvent.ProductId,
            domainEvent.ProductName,
            domainEvent.Barcode,
            StockAlertType.LowStock,
            domainEvent.CurrentStock,
            domainEvent.Threshold,
            StockAlertDeliveryStatus.Sent,
            recipientsNotified: string.Join(",", recipients),
            failureReason: null,
            occurredOn: DateTime.UtcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Enqueued low-stock alert {MailRequestId} for tenant {TenantId} product {ProductName} ({Barcode}) to {RecipientCount} recipient(s)",
            requestId, domainEvent.TenantId, domainEvent.ProductName, domainEvent.Barcode, recipients.Count);
    }
}
