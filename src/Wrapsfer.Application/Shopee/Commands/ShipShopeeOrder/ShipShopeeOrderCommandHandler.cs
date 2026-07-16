using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

internal sealed class ShipShopeeOrderCommandHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeGateway shopeeGateway,
    ShopeeConnectionTokenRefresher tokenRefresher,
    ShopeeOrderShipmentCompletionService completionService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<ShipShopeeOrderCommand, ShopeeOrderResponse>
{
    public async Task<Result<ShopeeOrderResponse>> Handle(
        ShipShopeeOrderCommand request, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (order is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotFound);
        }

        if (order.Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        if (order.HasUnresolvedItems)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ItemsNotLinked);
        }

        // Spec: IN_CANCEL (buyer requested cancellation, unresolved on Shopee) blocks
        // arranging new shipment but cancels nothing.
        if (string.Equals(order.ShopeeStatus, "IN_CANCEL", StringComparison.Ordinal))
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.CancellationRequested);
        }

        // ShipmentFailed with an arrangement timestamp means ship_order already succeeded
        // on Shopee's side and only local completion failed (tracking conflict, stock,
        // missing product). Re-running ship_order can never succeed here — Shopee would
        // reject the order as already arranged. Skip straight to the completion retry.
        if (order.Status == ShopeeOrderStatus.ShipmentFailed && order.ShipmentArrangedAt is not null)
        {
            return await ResumeAfterArrangedShipmentAsync(order, request, cancellationToken);
        }

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        // The order's local ReadyToShip/ShipmentFailed status can be stale relative to
        // Shopee if the shipment was already arranged there (Seller Centre, another
        // integration, or a prior attempt whose local status update was lost). Re-running
        // ship_order against Shopee in that state only errors out on Shopee's side.
        if (order.ShopeeStatus is "PROCESSED" or "SHIPPED" or "COMPLETED" or "TO_CONFIRM_RECEIVE")
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.AlreadyArrangedOnShopee);
        }

        ShopeeShipOrderRequest shipRequest = request.Method == "pickup"
            ? new ShopeeShipOrderRequest(
                order.OrderSn, new ShopeeShipOrderPickup(request.AddressId!.Value, request.PickupTimeId!), null)
            : new ShopeeShipOrderRequest(
                order.OrderSn, null, new ShopeeShipOrderDropoff(request.BranchId));

        Result shipResult = await shopeeGateway.ShipOrderAsync(
            connection.ShopId, connection.AccessToken, shipRequest, cancellationToken);
        if (shipResult.IsFailure)
        {
            Result failResult = order.MarkShipmentFailed(shipResult.Error.Description, DateTime.UtcNow);
            if (failResult.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ShipmentRequestFailed);
        }

        Result arrangeResult = order.MarkShipmentArranged(tenantContext.Username, DateTime.UtcNow);
        if (arrangeResult.IsFailure)
        {
            return Result<ShopeeOrderResponse>.Failure(arrangeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await TryCompleteWithTrackingAsync(order, connection, cancellationToken);

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(order));
    }

    /// <summary>
    /// ShipmentFailed with an arrangement timestamp means ship_order already succeeded on
    /// Shopee's side and only local completion failed (tracking conflict, stock, missing
    /// product). Re-running ship_order can never succeed here — Shopee would reject the
    /// order as already arranged — so skip straight to the completion retry.
    /// </summary>
    private async Task<Result<ShopeeOrderResponse>> ResumeAfterArrangedShipmentAsync(
        ShopeeOrder order, ShipShopeeOrderCommand request, CancellationToken cancellationToken)
    {
        Result arrangeResult = order.MarkShipmentArranged(tenantContext.Username, DateTime.UtcNow);
        if (arrangeResult.IsFailure)
        {
            return Result<ShopeeOrderResponse>.Failure(arrangeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is not null)
        {
            await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);
            await TryCompleteWithTrackingAsync(order, connection, cancellationToken);
        }

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(order));
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
