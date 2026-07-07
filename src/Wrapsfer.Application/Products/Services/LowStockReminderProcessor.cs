using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Products.Services;

public sealed class LowStockReminderProcessor(
    IProductRepository productRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    IStockAlertLogRepository stockAlertLogRepository,
    LowStockAlertService alertService,
    IOptions<ProductSettings> productSettings,
    ILogger<LowStockReminderProcessor> logger)
{
    public async Task<LowStockReminderRunSummary> RunAsync(CancellationToken cancellationToken)
    {
        int globalDefault = productSettings.Value.GlobalLowStockThreshold;
        int reminderIntervalHours = productSettings.Value.LowStockReminderIntervalHours;
        TimeSpan reminderInterval = TimeSpan.FromHours(reminderIntervalHours);
        int maxAlerts = productSettings.Value.MaxLowStockAlerts;

        IReadOnlyList<string> tenantIds = await productRepository.GetDistinctTenantIdsAsync(cancellationToken);
        if (tenantIds.Count == 0)
        {
            return new LowStockReminderRunSummary(0, 0);
        }

        IReadOnlyDictionary<string, int?> tenantDefaults =
            await tenantSettingsRepository.GetDefaultThresholdsByTenantIdsAsync(tenantIds, cancellationToken);

        int totalSent = 0;
        int totalSkipped = 0;
        DateTime now = DateTime.UtcNow;

        foreach (string tenantId in tenantIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            int tenantDefault = tenantDefaults.TryGetValue(tenantId, out int? d) && d.HasValue
                ? d.Value
                : globalDefault;

            IReadOnlyList<Product> candidates =
                await productRepository.GetLowStockCandidatesAsync(tenantId, tenantDefault, cancellationToken);

            if (candidates.Count == 0)
            {
                continue;
            }

            List<Guid> productIds = candidates.Select(p => p.Id).ToList();

            IReadOnlyDictionary<Guid, StockAlertLog> lastLowStock =
                await stockAlertLogRepository.GetLastSentAlertsByProductIdsAsync(
                    tenantId, productIds, StockAlertType.LowStock, cancellationToken);
            IReadOnlyDictionary<Guid, StockAlertLog> lastRecovery =
                await stockAlertLogRepository.GetLastSentAlertsByProductIdsAsync(
                    tenantId, productIds, StockAlertType.Recovered, cancellationToken);

            foreach (Product product in candidates)
            {
                if (!lastLowStock.TryGetValue(product.Id, out StockAlertLog? lowStockLog))
                {
                    // No prior low-stock alert sent — the initial event flow handles the first send.
                    continue;
                }

                if (now - lowStockLog.OccurredOn < reminderInterval)
                {
                    totalSkipped++;
                    continue;
                }

                if (lastRecovery.TryGetValue(product.Id, out StockAlertLog? recoveryLog)
                    && recoveryLog.OccurredOn > lowStockLog.OccurredOn)
                {
                    // Cycle closed by a recovery — wait for a new low-stock event to start a new cycle.
                    continue;
                }

                int effectiveThreshold = product.LowStockThreshold ?? tenantDefault;
                LowStockAlertContext context = new(
                    tenantId,
                    product.Id,
                    product.Name,
                    product.Barcode,
                    product.StockQuantity,
                    effectiveThreshold);

                try
                {
                    LowStockAlertOutcome outcome =
                        await alertService.SendAsync(context, reminderInterval, maxAlerts, cancellationToken);
                    if (outcome == LowStockAlertOutcome.Sent)
                    {
                        totalSent++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to send low-stock reminder for tenant {TenantId} product {ProductId}",
                        tenantId, product.Id);
                }
            }
        }

        return new LowStockReminderRunSummary(totalSent, totalSkipped);
    }
}
