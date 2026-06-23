using Microsoft.Extensions.Logging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Products.Services;

public sealed class LowStockAlertService(
    IMailer mailer,
    ITenantSettingsRepository tenantSettingsRepository,
    IStockAlertLogRepository stockAlertLogRepository,
    IUnitOfWork unitOfWork,
    ILogger<LowStockAlertService> logger)
{
    public async Task<LowStockAlertOutcome> SendAsync(
        LowStockAlertContext context,
        TimeSpan dedupeWindow,
        int maxAlerts,
        CancellationToken cancellationToken = default)
    {
        StockAlertLog? lastSent = await stockAlertLogRepository.GetLastAlertAsync(
            context.TenantId,
            context.ProductId,
            StockAlertType.LowStock,
            StockAlertDeliveryStatus.Sent,
            cancellationToken);

        if (lastSent is not null && DateTime.UtcNow - lastSent.OccurredOn < dedupeWindow)
        {
            logger.LogInformation(
                "Suppressing low-stock alert for tenant {TenantId} product {ProductId} — last sent {OccurredOn}",
                context.TenantId, context.ProductId, lastSent.OccurredOn);

            stockAlertLogRepository.Add(StockAlertLog.Create(
                context.TenantId,
                context.ProductId,
                context.ProductName,
                context.Barcode,
                StockAlertType.LowStock,
                context.CurrentStock,
                context.Threshold,
                StockAlertDeliveryStatus.Suppressed,
                null,
                "DedupeWindow",
                DateTime.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return LowStockAlertOutcome.Suppressed;
        }

        int sentThisEpisode = await stockAlertLogRepository.CountSentLowStockAlertsInCurrentEpisodeAsync(
            context.TenantId,
            context.ProductId,
            cancellationToken);

        if (sentThisEpisode >= maxAlerts)
        {
            logger.LogInformation(
                "Suppressing low-stock alert for tenant {TenantId} product {ProductId} — max alerts reached ({SentThisEpisode}/{MaxAlerts}) for current episode",
                context.TenantId, context.ProductId, sentThisEpisode, maxAlerts);

            stockAlertLogRepository.Add(StockAlertLog.Create(
                context.TenantId,
                context.ProductId,
                context.ProductName,
                context.Barcode,
                StockAlertType.LowStock,
                context.CurrentStock,
                context.Threshold,
                StockAlertDeliveryStatus.Suppressed,
                null,
                "MaxAlertsReached",
                DateTime.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return LowStockAlertOutcome.MaxReached;
        }

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(context.TenantId, cancellationToken);

        List<string> recipients = settings?.Recipients
            .Where(r => r.IsActive)
            .Select(r => r.Email)
            .ToList() ?? [];

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "No active recipients configured for tenant {TenantId} — skipping low stock alert for product {ProductName} ({Barcode})",
                context.TenantId, context.ProductName, context.Barcode);

            stockAlertLogRepository.Add(StockAlertLog.Create(
                context.TenantId,
                context.ProductId,
                context.ProductName,
                context.Barcode,
                StockAlertType.LowStock,
                context.CurrentStock,
                context.Threshold,
                StockAlertDeliveryStatus.Failed,
                null,
                "NoRecipients",
                DateTime.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return LowStockAlertOutcome.NoRecipients;
        }

        MailMessage mail = new()
        {
            To = recipients,
            TemplateName = "low-stock-alert",
            Tokens = new Dictionary<string, object?>
            {
                ["ProductName"] = context.ProductName,
                ["Barcode"] = context.Barcode,
                ["CurrentStock"] = context.CurrentStock,
                ["Threshold"] = context.Threshold
            }
        };

        MailRequestId requestId = await mailer.SendAsync(mail, cancellationToken);

        stockAlertLogRepository.Add(StockAlertLog.Create(
            context.TenantId,
            context.ProductId,
            context.ProductName,
            context.Barcode,
            StockAlertType.LowStock,
            context.CurrentStock,
            context.Threshold,
            StockAlertDeliveryStatus.Sent,
            string.Join(",", recipients),
            null,
            DateTime.UtcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Enqueued low-stock alert {MailRequestId} for tenant {TenantId} product {ProductName} ({Barcode}) to {RecipientCount} recipient(s)",
            requestId, context.TenantId, context.ProductName, context.Barcode, recipients.Count);

        return LowStockAlertOutcome.Sent;
    }
}
