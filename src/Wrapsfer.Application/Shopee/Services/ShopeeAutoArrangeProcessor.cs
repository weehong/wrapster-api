using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Sweeps tenants with an Active fulfillment delegation: re-ingests NeedsLinking orders
/// so late-added products/links unblock them, then arranges shipment for ReadyToShip
/// orders using the delegation's preferred method with fallback. Transient failures
/// leave orders ReadyToShip for the next cycle; only a missing concrete shipping option
/// or Shopee's rejection of ship_order marks ShipmentFailed. Orders past their ship-by
/// date are skipped — Shopee rejects them and the partner UI surfaces them as overdue.
/// </summary>
public sealed class ShopeeAutoArrangeProcessor(
    IFulfillmentDelegationRepository delegationRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeOrderRepository orderRepository,
    ShopeeOrderIngestionService ingestionService,
    ShopeeOrderArrangeService arrangeService,
    ShopeeConnectionTokenRefresher tokenRefresher,
    IShopeeGateway shopeeGateway,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeAutoArrangeProcessor> logger)
{
    public const string SystemActor = "system:auto-arrange";

    public async Task<ShopeeAutoArrangeRunSummary> RunAsync(
        int orderBatchSize, CancellationToken cancellationToken)
    {
        int tenantsExamined = 0;
        int relinkAttempts = 0;
        int ordersArranged = 0;
        int ordersFailed = 0;
        int ordersSkipped = 0;

        IReadOnlyList<FulfillmentDelegation> delegations = await delegationRepository.ListAsync(
            FulfillmentDelegationStatus.Active, cancellationToken);

        foreach (FulfillmentDelegation delegation in delegations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tenantsExamined++;
            try
            {
                (int relinked, int arranged, int failed, int skipped) = await ProcessTenantAsync(
                    delegation, orderBatchSize, cancellationToken);
                relinkAttempts += relinked;
                ordersArranged += arranged;
                ordersFailed += failed;
                ordersSkipped += skipped;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Auto-arrange sweep failed for tenant {TenantId}", delegation.TenantId);
                unitOfWork.ClearChangeTracker();
            }
        }

        ShopeeAutoArrangeRunSummary summary = new(
            tenantsExamined, relinkAttempts, ordersArranged, ordersFailed, ordersSkipped);
        logger.LogInformation(
            "Auto-arrange run: {Tenants} tenants, {Relinks} relink attempts, {Arranged} arranged, "
            + "{Failed} failed, {Skipped} skipped",
            summary.TenantsExamined, summary.RelinkAttempts, summary.OrdersArranged,
            summary.OrdersFailed, summary.OrdersSkipped);
        return summary;
    }

    private async Task<(int Relinked, int Arranged, int Failed, int Skipped)> ProcessTenantAsync(
        FulfillmentDelegation delegation, int orderBatchSize, CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            delegation.TenantId, cancellationToken);
        if (connection is null)
        {
            logger.LogWarning(
                "Auto-arrange: tenant {TenantId} has an active delegation but no Shopee connection",
                delegation.TenantId);
            return (0, 0, 0, 0);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        int relinked = await RelinkNeedsLinkingOrdersAsync(connection, orderBatchSize, cancellationToken);

        (int arranged, int failed, int skipped) = await ArrangeReadyOrdersAsync(
            delegation, connection, orderBatchSize, cancellationToken);
        return (relinked, arranged, failed, skipped);
    }

    private async Task<int> RelinkNeedsLinkingOrdersAsync(
        ShopeeShopConnection connection, int orderBatchSize, CancellationToken cancellationToken)
    {
        (IReadOnlyList<ShopeeOrder> orders, _) = await orderRepository.ListAsync(
            connection.TenantId, ShopeeOrderStatus.NeedsLinking, null, 1, orderBatchSize,
            cancellationToken);

        int attempts = 0;
        foreach (ShopeeOrder order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            Result ingestResult = await ingestionService.IngestOrderAsync(
                connection, order.OrderSn, cancellationToken);
            if (ingestResult.IsFailure)
            {
                logger.LogWarning(
                    "Auto-arrange relink of order {OrderSn} failed for tenant {TenantId}: {ErrorCode}",
                    order.OrderSn, connection.TenantId, ingestResult.Error.Code);
            }
        }

        return attempts;
    }

    private async Task<(int Arranged, int Failed, int Skipped)> ArrangeReadyOrdersAsync(
        FulfillmentDelegation delegation, ShopeeShopConnection connection, int orderBatchSize,
        CancellationToken cancellationToken)
    {
        // Listed AFTER the relink pass so orders it just unblocked arrange in this cycle.
        (IReadOnlyList<ShopeeOrder> listedOrders, _) = await orderRepository.ListAsync(
            connection.TenantId, ShopeeOrderStatus.ReadyToShip, null, 1, orderBatchSize,
            cancellationToken);

        int arranged = 0;
        int failed = 0;
        int skipped = 0;
        foreach (ShopeeOrder listedOrder in listedOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Re-fetch per order: a ClearChangeTracker during the relink pass (ingestion
            // failure path) detaches list results, and mutating a detached order would
            // silently not persist. Same pattern as the reconciliation processor.
            ShopeeOrder? order = await orderRepository.GetByIdAsync(
                listedOrder.Id, connection.TenantId, cancellationToken);
            if (order is null || order.Status != ShopeeOrderStatus.ReadyToShip)
            {
                continue;
            }

            if (order.ShipByDate is DateTime shipBy && shipBy < DateTime.UtcNow)
            {
                skipped++;
                continue;
            }

            Result<ShopeeShippingParameter> parameterResult = await shopeeGateway.GetShippingParameterAsync(
                connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
            if (parameterResult.IsFailure)
            {
                // Transient: leave ReadyToShip; the next cycle retries until ship-by passes.
                logger.LogWarning(
                    "Auto-arrange shipping-parameter fetch failed for order {OrderSn}: {ErrorCode}",
                    order.OrderSn, parameterResult.Error.Code);
                continue;
            }

            Result<ShopeeShipOrderRequest> selection = ShopeeAutoArrangeParamSelector.Select(
                order.OrderSn, delegation.DefaultShippingMethod, parameterResult.Value);
            if (selection.IsFailure)
            {
                Result failResult = order.MarkShipmentFailed(
                    selection.Error.Description, DateTime.UtcNow);
                if (failResult.IsSuccess)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                failed++;
                continue;
            }

            Result arrangeResult = await arrangeService.ArrangeAsync(
                order, connection, selection.Value, SystemActor, cancellationToken);
            if (arrangeResult.IsSuccess)
            {
                arranged++;
            }
            else
            {
                failed++;
                logger.LogWarning(
                    "Auto-arrange failed for order {OrderSn} of tenant {TenantId}: {ErrorCode}",
                    order.OrderSn, connection.TenantId, arrangeResult.Error.Code);
            }
        }

        return (arranged, failed, skipped);
    }
}
