using FluentAssertions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeAutoArrangeParamSelectorTests
{
    private const string OrderSn = "SN1";

    private static ShopeePickupAddress Address(long id, params ShopeePickupTimeSlot[] slots) =>
        new(id, $"address-{id}", slots);

    private static ShopeePickupTimeSlot Slot(string id, int day) =>
        new(id, new DateTime(2026, 7, day, 0, 0, 0, DateTimeKind.Utc), null);

    [Fact]
    public void Select_PreferDropoff_DropoffSupported_UsesFirstBranch()
    {
        ShopeeShippingParameter parameter = new(
            SupportsPickup: true, SupportsDropoff: true,
            [Address(1, Slot("t1", 25))],
            [new ShopeeDropoffBranch(77, "branch"), new ShopeeDropoffBranch(88, "other")]);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pickup.Should().BeNull();
        result.Value.Dropoff!.BranchId.Should().Be(77);
    }

    [Fact]
    public void Select_PreferDropoff_NoBranchesListed_UsesBranchlessDropoff()
    {
        ShopeeShippingParameter parameter = new(false, true, [], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff!.BranchId.Should().BeNull();
    }

    [Fact]
    public void Select_PreferPickup_PickupSupported_UsesEarliestSlot()
    {
        ShopeeShippingParameter parameter = new(
            true, true, [Address(5, Slot("late", 28), Slot("early", 25))], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff.Should().BeNull();
        result.Value.Pickup!.AddressId.Should().Be(5);
        result.Value.Pickup.PickupTimeId.Should().Be("early");
    }

    [Fact]
    public void Select_PreferPickup_FirstAddressHasNoSlots_UsesNextAddress()
    {
        ShopeeShippingParameter parameter = new(
            true, false, [Address(1), Address(2, Slot("t2", 26))], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pickup!.AddressId.Should().Be(2);
    }

    [Fact]
    public void Select_PreferPickup_PickupUnsupported_FallsBackToDropoff()
    {
        ShopeeShippingParameter parameter = new(
            false, true, [], [new ShopeeDropoffBranch(9, "branch")]);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff!.BranchId.Should().Be(9);
    }

    [Fact]
    public void Select_PreferPickup_PickupSupportedButNoSlots_FallsBackToDropoff()
    {
        ShopeeShippingParameter parameter = new(true, true, [Address(1)], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff.Should().NotBeNull();
    }

    [Fact]
    public void Select_PreferDropoff_DropoffUnsupported_FallsBackToPickup()
    {
        ShopeeShippingParameter parameter = new(
            true, false, [Address(3, Slot("t3", 27))], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pickup!.AddressId.Should().Be(3);
    }

    [Fact]
    public void Select_NeitherMethodSupported_ReturnsNoUsableShippingOption()
    {
        ShopeeShippingParameter parameter = new(false, false, [], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NoUsableShippingOption);
    }

    [Fact]
    public void Select_PickupOnlyWithNoSlotsAnywhere_ReturnsNoUsableShippingOption()
    {
        ShopeeShippingParameter parameter = new(true, false, [Address(1), Address(2)], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NoUsableShippingOption);
    }
}
