using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

internal sealed class ShipShopeeOrderCommandHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    ShopeeConnectionTokenRefresher tokenRefresher,
    ShopeeOrderArrangeService arrangeService,
    ITenantContext tenantContext) : ICommandHandler<ShipShopeeOrderCommand, ShopeeOrderResponse>
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

        // Pre-checks duplicate the arrange service's guards on purpose: they preserve the
        // HTTP flow's error precedence (a bad order status must surface before a missing
        // connection, which is only loaded afterwards).
        if (order.Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        if (order.HasUnresolvedItems)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ItemsNotLinked);
        }

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

        Result arrangeResult = await arrangeService.ArrangeAsync(
            order, connection, shipRequest, tenantContext.Username, cancellationToken);
        if (arrangeResult.IsFailure)
        {
            return Result<ShopeeOrderResponse>.Failure(arrangeResult.Error);
        }

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(order));
    }
}
