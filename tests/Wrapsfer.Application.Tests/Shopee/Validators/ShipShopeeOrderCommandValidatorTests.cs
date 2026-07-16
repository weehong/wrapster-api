using FluentValidation.Results;
using Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

namespace Wrapsfer.Application.Tests.Shopee.Validators;

public class ShipShopeeOrderCommandValidatorTests
{
    private readonly ShipShopeeOrderCommandValidator _validator = new();

    private static ShipShopeeOrderCommand CreateCommand(
        string tenantId = "test-tenant",
        Guid? orderId = null,
        string method = "dropoff",
        long? addressId = null,
        string? pickupTimeId = null,
        long? branchId = null) =>
        new(tenantId, orderId ?? Guid.NewGuid(), method, addressId, pickupTimeId, branchId);

    [Fact]
    public void Validate_WhenDropoffValid_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(CreateCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenPickupWithAddressAndTime_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(
            CreateCommand(method: "pickup", addressId: 1, pickupTimeId: "slot-1"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTenantIdEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(tenantId: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TenantId");
    }

    [Fact]
    public void Validate_WhenOrderIdEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(orderId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrderId");
    }

    [Fact]
    public void Validate_WhenMethodInvalid_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(method: "courier"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Method");
    }

    [Fact]
    public void Validate_WhenPickupWithoutAddressIdOrPickupTimeId_HasValidationErrors()
    {
        ValidationResult result = _validator.Validate(CreateCommand(method: "pickup"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AddressId");
        result.Errors.Should().Contain(e => e.PropertyName == "PickupTimeId");
    }
}
