using Microsoft.Extensions.Logging;
using Wrapster.Application.Abstractions;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Events;
using Wrapster.Domain.Repositories;
using Wrapster.Mailing.Abstractions;
using TenantSettingsEntity = Wrapster.Domain.Entities.TenantSettings;

namespace Wrapster.Application.Products.EventHandlers;

internal sealed class StockRecoveredEventHandler(
    IMailer mailer,
    ITenantSettingsRepository tenantSettingsRepository,
    IStockAlertLogRepository stockAlertLogRepository,
    IUnitOfWork unitOfWork,
    ILogger<StockRecoveredEventHandler> logger)
    : IDomainEventHandler<StockRecoveredEvent>
{
    public async Task Handle(DomainEventNotification<StockRecoveredEvent> notification,
        CancellationToken cancellationToken)
    {
        StockRecoveredEvent domainEvent = notification.DomainEvent;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(domainEvent.TenantId, cancellationToken);

        List<string> recipients = settings?.Recipients
            .Where(r => r.IsActive)
            .Select(r => r.Email)
            .ToList() ?? [];

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "No active recipients configured for tenant {TenantId} — skipping stock recovery notice for product {ProductName} ({Barcode})",
                domainEvent.TenantId, domainEvent.ProductName, domainEvent.Barcode);

            stockAlertLogRepository.Add(StockAlertLog.Create(
                domainEvent.TenantId,
                domainEvent.ProductId,
                domainEvent.ProductName,
                domainEvent.Barcode,
                StockAlertType.Recovered,
                domainEvent.CurrentStock,
                domainEvent.Threshold,
                StockAlertDeliveryStatus.Failed,
                null,
                "NoRecipients",
                DateTime.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        MailMessage mail = new()
        {
            To = recipients,
            TemplateName = "stock-recovered",
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
            StockAlertType.Recovered,
            domainEvent.CurrentStock,
            domainEvent.Threshold,
            StockAlertDeliveryStatus.Sent,
            string.Join(",", recipients),
            null,
            DateTime.UtcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Enqueued stock-recovered notice {MailRequestId} for tenant {TenantId} product {ProductName} ({Barcode}) to {RecipientCount} recipient(s)",
            requestId, domainEvent.TenantId, domainEvent.ProductName, domainEvent.Barcode, recipients.Count);
    }
}
