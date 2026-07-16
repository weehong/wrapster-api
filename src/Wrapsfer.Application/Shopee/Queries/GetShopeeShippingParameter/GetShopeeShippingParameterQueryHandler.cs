using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeShippingParameter;

internal sealed class GetShopeeShippingParameterQueryHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeGateway shopeeGateway,
    ShopeeConnectionTokenRefresher tokenRefresher)
    : IQueryHandler<GetShopeeShippingParameterQuery, ShopeeShippingParameterResponse>
{
    public async Task<Result<ShopeeShippingParameterResponse>> Handle(
        GetShopeeShippingParameterQuery request, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (order is null)
        {
            return Result<ShopeeShippingParameterResponse>.Failure(ShopeeOrderErrors.NotFound);
        }

        if (order.Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result<ShopeeShippingParameterResponse>.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeShippingParameterResponse>.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        Result<ShopeeShippingParameter> parameterResult = await shopeeGateway.GetShippingParameterAsync(
            connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
        if (parameterResult.IsFailure)
        {
            return Result<ShopeeShippingParameterResponse>.Failure(parameterResult.Error);
        }

        return Result<ShopeeShippingParameterResponse>.Success(Map(parameterResult.Value));
    }

    private static ShopeeShippingParameterResponse Map(ShopeeShippingParameter parameter) =>
        new(
            parameter.SupportsPickup,
            parameter.SupportsDropoff,
            parameter.PickupAddresses.Select(MapAddress).ToList(),
            parameter.DropoffBranches.Select(MapBranch).ToList());

    private static ShopeePickupAddressResponse MapAddress(ShopeePickupAddress address) =>
        new(address.AddressId, address.Address, address.TimeSlots.Select(MapTimeSlot).ToList());

    private static ShopeePickupTimeSlotResponse MapTimeSlot(ShopeePickupTimeSlot slot) =>
        new(slot.PickupTimeId, slot.Date, slot.TimeText);

    private static ShopeeDropoffBranchResponse MapBranch(ShopeeDropoffBranch branch) =>
        new(branch.BranchId, branch.Address);
}
