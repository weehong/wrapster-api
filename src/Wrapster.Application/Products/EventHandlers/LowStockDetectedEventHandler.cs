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

internal sealed class LowStockDetectedEventHandler(
    IEmailService emailService,
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

        LowStockAlertTemplate template = new(
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
                    "Failed to send low stock alert email to {Email} for product {ProductName}: {Error}",
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
            StockAlertType.LowStock,
            domainEvent.CurrentStock,
            domainEvent.Threshold,
            status,
            recipientsNotified: sentTo.Count > 0 ? string.Join(",", sentTo) : null,
            failureReason: failures.Count > 0 ? string.Join(";", failures) : null,
            occurredOn: DateTime.UtcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Low stock alert processed for tenant {TenantId} product {ProductName} ({Barcode}) — status: {Status}, sent: {SentCount}, failed: {FailedCount}",
            domainEvent.TenantId, domainEvent.ProductName, domainEvent.Barcode, status, sentTo.Count, failures.Count);
    }
}
