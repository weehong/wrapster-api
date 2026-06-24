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
                StockAlertDeliveryStatus.Suppressed,
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
