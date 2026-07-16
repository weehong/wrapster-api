using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Safety net behind the webhook path: sweeps each connected shop's recently-updated
/// orders through the same ingestion path, and re-polls tracking numbers for orders
/// stuck in AwaitingTracking. Heals dropped pushes and exhausted webhook retries.
/// </summary>
public sealed class ShopeeOrderReconciliationProcessor(
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeOrderRepository orderRepository,
    IShopeeGateway shopeeGateway,
    ShopeeOrderIngestionService ingestionService,
    ShopeeOrderShipmentCompletionService completionService,
    ShopeeConnectionTokenRefresher tokenRefresher,
    ILogger<ShopeeOrderReconciliationProcessor> logger)
{
    private const int OrderListPageSize = 50;
    private const int TrackingRetryBatchSize = 100;

    public async Task RunAsync(
        int windowHours, int trackingRetryThresholdMinutes, CancellationToken cancellationToken)
    {
        IReadOnlyList<ShopeeShopConnection> connections =
            await connectionRepository.ListAllAsync(cancellationToken);

        foreach (ShopeeShopConnection connection in connections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await SweepConnectionAsync(connection, windowHours, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Shopee order reconciliation failed for tenant {TenantId}", connection.TenantId);
            }
        }

        await RetryStuckTrackingAsync(trackingRetryThresholdMinutes, cancellationToken);
    }

    private async Task SweepConnectionAsync(
        ShopeeShopConnection connection, int windowHours, CancellationToken cancellationToken)
    {
        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        DateTime to = DateTime.UtcNow;
        DateTime from = to.AddHours(-windowHours);
        string? cursor = null;

        do
        {
            Result<ShopeeOrderList> pageResult = await shopeeGateway.GetOrderListAsync(
                connection.ShopId, connection.AccessToken, from, to, cursor, OrderListPageSize,
                cancellationToken);
            if (pageResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee order list fetch failed for tenant {TenantId}: {ErrorCode}",
                    connection.TenantId, pageResult.Error.Code);
                return;
            }

            ShopeeOrderList page = pageResult.Value;
            foreach (string orderSn in page.OrderSns)
            {
                Result ingestResult =
                    await ingestionService.IngestOrderAsync(connection, orderSn, cancellationToken);
                if (ingestResult.IsFailure)
                {
                    logger.LogWarning(
                        "Shopee order {OrderSn} reconciliation ingest failed for tenant {TenantId}: {ErrorCode}",
                        orderSn, connection.TenantId, ingestResult.Error.Code);
                }
            }

            cursor = page.HasMore ? page.NextCursor : null;
        } while (!string.IsNullOrEmpty(cursor));
    }

    private async Task RetryStuckTrackingAsync(
        int trackingRetryThresholdMinutes, CancellationToken cancellationToken)
    {
        DateTime arrangedBefore = DateTime.UtcNow.AddMinutes(-trackingRetryThresholdMinutes);
        IReadOnlyList<ShopeeOrder> stuckOrders = await orderRepository.ListAwaitingTrackingAsync(
            arrangedBefore, TrackingRetryBatchSize, cancellationToken);

        foreach (ShopeeOrder order in stuckOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShopeeShopConnection? connection =
                await connectionRepository.GetByTenantIdAsync(order.TenantId, cancellationToken);
            if (connection is null)
            {
                continue;
            }

            await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);
            Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
            if (trackingResult.IsFailure || string.IsNullOrWhiteSpace(trackingResult.Value))
            {
                continue;
            }

            Result completeResult = await completionService.CompleteAsync(
                order, trackingResult.Value, cancellationToken);
            if (completeResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee order {OrderSn} tracking completion failed: {ErrorCode}",
                    order.OrderSn, completeResult.Error.Code);
            }
        }
    }
}
