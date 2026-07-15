using FluentValidation.Results;
using Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

namespace Wrapsfer.Application.Tests.Shopee.Validators;

public class CreateShopeeLinkedProductCommandValidatorTests
{
    private readonly CreateShopeeLinkedProductCommandValidator _validator = new();

    private static CreateShopeeLinkedProductCommand CreateCommand(
        string tenantId = "test-tenant",
        long shopeeItemId = 1001,
        long shopeeModelId = 0,
        string barcode = "BC-001",
        string name = "Widget",
        string? skuCode = "SKU-1",
        decimal cost = 9.99m,
        int stockQuantity = 50,
        int? lowStockThreshold = 10) =>
        new(tenantId, shopeeItemId, shopeeModelId, barcode, name, skuCode, cost, stockQuantity,
            lowStockThreshold);

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(CreateCommand());

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
    public void Validate_WhenShopeeItemIdNotPositive_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(shopeeItemId: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ShopeeItemId");
    }

    [Fact]
    public void Validate_WhenShopeeModelIdNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(shopeeModelId: -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ShopeeModelId");
    }

    [Fact]
    public void Validate_WhenBarcodeEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(barcode: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Barcode");
    }

    [Fact]
    public void Validate_WhenNameEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(name: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhenCostNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(cost: -1m));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Cost");
    }

    [Fact]
    public void Validate_WhenStockQuantityNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(stockQuantity: -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StockQuantity");
    }

    [Fact]
    public void Validate_WhenLowStockThresholdNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(lowStockThreshold: -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LowStockThreshold");
    }

    [Fact]
    public void Validate_WhenLowStockThresholdNull_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(CreateCommand(lowStockThreshold: null));

        result.IsValid.Should().BeTrue();
    }
}
