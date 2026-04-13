using Microsoft.Extensions.Logging;
using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Email;
using Wrapster.Application.Email.Templates;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Events;
using Wrapster.Domain.Repositories;
using TenantSettingsEntity = Wrapster.Domain.Entities.TenantSettings;

namespace Wrapster.Application.Products.EventHandlers;

internal sealed class StockRecoveredEventHandler(
    IEmailService emailService,
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

        StockRecoveryTemplate template = new(
            domainEvent.ProductName,
            domainEvent.Barcode,
            domainEvent.CurrentStock,
            domainEvent.Threshold);

        List<string> sentTo = [];
        List<string> failures = [];

        foreach (string recipient in recipients)
        {
            Result sendResult = await emailService.SendAsync(recipient, template, cancellationToken);
            if (sendResult.IsSuccess)
            {
                sentTo.Add(recipient);
            }
            else
            {
                failures.Add($"{recipient}:{sendResult.Error.Code}");
                logger.LogError(
                    "Failed to send stock recovery email to {Email} for product {ProductName}: {Error}",
                    recipient, domainEvent.ProductName, sendResult.Error.Description);
            }
        }

        StockAlertDeliveryStatus status = sentTo.Count > 0
            ? StockAlertDeliveryStatus.Sent
            : StockAlertDeliveryStatus.Failed;

        stockAlertLogRepository.Add(StockAlertLog.Create(
            domainEvent.TenantId,
            domainEvent.ProductId,
            domainEvent.ProductName,
            domainEvent.Barcode,
            StockAlertType.Recovered,
            domainEvent.CurrentStock,
            domainEvent.Threshold,
            status,
            sentTo.Count > 0 ? string.Join(",", sentTo) : null,
            failures.Count > 0 ? string.Join(";", failures) : null,
            DateTime.UtcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Stock recovery notice processed for tenant {TenantId} product {ProductName} ({Barcode}) — status: {Status}, sent: {SentCount}, failed: {FailedCount}",
            domainEvent.TenantId, domainEvent.ProductName, domainEvent.Barcode, status, sentTo.Count, failures.Count);
    }
}
