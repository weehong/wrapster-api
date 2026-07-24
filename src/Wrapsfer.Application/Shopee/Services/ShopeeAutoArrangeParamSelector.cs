using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Picks concrete ship_order parameters for auto-arrange: preferred method first, the
/// other method as fallback. Dropoff uses the first branch (branchless when Shopee lists
/// none); pickup uses the first address exposing a time slot, taking its earliest slot.
/// No concrete option from either method is a permanent failure the caller records.
/// </summary>
public static class ShopeeAutoArrangeParamSelector
{
    public static Result<ShopeeShipOrderRequest> Select(
        string orderSn, FulfillmentShippingMethod preferredMethod, ShopeeShippingParameter parameter)
    {
        FulfillmentShippingMethod[] methodsInOrder = preferredMethod == FulfillmentShippingMethod.Pickup
            ? [FulfillmentShippingMethod.Pickup, FulfillmentShippingMethod.Dropoff]
            : [FulfillmentShippingMethod.Dropoff, FulfillmentShippingMethod.Pickup];

        foreach (FulfillmentShippingMethod method in methodsInOrder)
        {
            ShopeeShipOrderRequest? request = method == FulfillmentShippingMethod.Dropoff
                ? TryDropoff(orderSn, parameter)
                : TryPickup(orderSn, parameter);
            if (request is not null)
            {
                return Result<ShopeeShipOrderRequest>.Success(request);
            }
        }

        return Result<ShopeeShipOrderRequest>.Failure(ShopeeOrderErrors.NoUsableShippingOption);
    }

    private static ShopeeShipOrderRequest? TryDropoff(string orderSn, ShopeeShippingParameter parameter)
    {
        if (!parameter.SupportsDropoff)
        {
            return null;
        }

        long? branchId = parameter.DropoffBranches.Count > 0
            ? parameter.DropoffBranches[0].BranchId
            : null;
        return new ShopeeShipOrderRequest(orderSn, null, new ShopeeShipOrderDropoff(branchId));
    }

    private static ShopeeShipOrderRequest? TryPickup(string orderSn, ShopeeShippingParameter parameter)
    {
        if (!parameter.SupportsPickup)
        {
            return null;
        }

        foreach (ShopeePickupAddress address in parameter.PickupAddresses)
        {
            ShopeePickupTimeSlot? slot = address.TimeSlots.OrderBy(s => s.Date).FirstOrDefault();
            if (slot is null)
            {
                continue;
            }

            return new ShopeeShipOrderRequest(
                orderSn, new ShopeeShipOrderPickup(address.AddressId, slot.PickupTimeId), null);
        }

        return null;
    }
}
