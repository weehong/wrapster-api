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

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

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

        // Opportunistic: Shopee often assigns the tracking number within seconds. A failure
        // here is not an error — the tracking push or reconciliation completes it later.
        Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
            connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
        if (trackingResult.IsSuccess && !string.IsNullOrWhiteSpace(trackingResult.Value))
        {
            await completionService.CompleteAsync(order, trackingResult.Value, cancellationToken);
        }

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(order));
    }
}
