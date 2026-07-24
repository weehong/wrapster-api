using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Arranges a Shopee shipment for an order: guards the local and Shopee status, calls
/// ship_order, records the arranging actor, and opportunistically completes with a
/// tracking number. Shared by the partner-facing ship command (actor = username) and the
/// auto-arrange job (actor = system). Callers load the connection and refresh its token.
/// </summary>
public sealed class ShopeeOrderArrangeService(
    IShopeeGateway shopeeGateway,
    ShopeeOrderShipmentCompletionService completionService,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> ArrangeAsync(
        ShopeeOrder order,
        ShopeeShopConnection connection,
        ShopeeShipOrderRequest shipRequest,
        string? arrangedBy,
        CancellationToken cancellationToken)
    {
        if (order.Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        if (order.HasUnresolvedItems)
        {
            return Result.Failure(ShopeeOrderErrors.ItemsNotLinked);
        }

        // Spec: IN_CANCEL (buyer requested cancellation, unresolved on Shopee) blocks
        // arranging new shipment but cancels nothing.
        if (string.Equals(order.ShopeeStatus, "IN_CANCEL", StringComparison.Ordinal))
        {
            return Result.Failure(ShopeeOrderErrors.CancellationRequested);
        }

        // ShipmentFailed with an arrangement timestamp means ship_order already succeeded
        // on Shopee's side and only local completion failed (tracking conflict, stock,
        // missing product). Re-running ship_order can never succeed here — Shopee would
        // reject the order as already arranged. Skip straight to the completion retry.
        if (order.Status == ShopeeOrderStatus.ShipmentFailed && order.ShipmentArrangedAt is not null)
        {
            return await ResumeAfterArrangedShipmentAsync(order, connection, arrangedBy, cancellationToken);
        }

        // The order's local ReadyToShip/ShipmentFailed status can be stale relative to
        // Shopee if the shipment was already arranged there (Seller Centre, another
        // integration, or a prior attempt whose local status update was lost). Re-running
        // ship_order against Shopee in that state only errors out on Shopee's side.
        if (order.ShopeeStatus is "PROCESSED" or "SHIPPED" or "COMPLETED" or "TO_CONFIRM_RECEIVE")
        {
            return Result.Failure(ShopeeOrderErrors.AlreadyArrangedOnShopee);
        }

        Result shipResult = await shopeeGateway.ShipOrderAsync(
            connection.ShopId, connection.AccessToken, shipRequest, cancellationToken);
        if (shipResult.IsFailure)
        {
            Result failResult = order.MarkShipmentFailed(shipResult.Error.Description, DateTime.UtcNow);
            if (failResult.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure(ShopeeOrderErrors.ShipmentRequestFailed);
        }

        Result arrangeResult = order.MarkShipmentArranged(arrangedBy, DateTime.UtcNow);
        if (arrangeResult.IsFailure)
        {
            return Result.Failure(arrangeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await TryCompleteWithTrackingAsync(order, connection, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ResumeAfterArrangedShipmentAsync(
        ShopeeOrder order, ShopeeShopConnection connection, string? arrangedBy,
        CancellationToken cancellationToken)
    {
        Result arrangeResult = order.MarkShipmentArranged(arrangedBy, DateTime.UtcNow);
        if (arrangeResult.IsFailure)
        {
            return Result.Failure(arrangeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await TryCompleteWithTrackingAsync(order, connection, cancellationToken);

        return Result.Success();
    }

    // Opportunistic: Shopee often assigns the tracking number within seconds. A failure
    // here is not an error — the tracking push or reconciliation completes it later.
    private async Task TryCompleteWithTrackingAsync(
        ShopeeOrder order, ShopeeShopConnection connection, CancellationToken cancellationToken)
    {
        Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
            connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
        if (trackingResult.IsSuccess && !string.IsNullOrWhiteSpace(trackingResult.Value))
        {
            await completionService.CompleteAsync(order, trackingResult.Value, cancellationToken);
        }
    }
}
