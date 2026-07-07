using FluentValidation.Results;
using Wrapsfer.Application.Products.Commands.CreateProduct;
using Wrapsfer.Application.Products.Common;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Tests.Products.Validators;

public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        CreateProductCommand command = new("BC-001", "Widget", "SKU-1", ProductType.Single, 9.99m, 50, 10);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBarcodeEmpty_HasValidationError()
    {
        CreateProductCommand command = new("", "Widget", null, ProductType.Single, 9.99m, 50, null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Barcode");
    }

    [Fact]
    public void Validate_WhenNameEmpty_HasValidationError()
    {
        CreateProductCommand command = new("BC-001", "", null, ProductType.Single, 9.99m, 50, null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhenCostNegative_HasValidationError()
    {
        CreateProductCommand command = new("BC-001", "Widget", null, ProductType.Single, -1m, 50, null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Cost");
    }

    [Fact]
    public void Validate_WhenStockQuantityNegative_HasValidationError()
    {
        CreateProductCommand command = new("BC-001", "Widget", null, ProductType.Single, 9.99m, -1, null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StockQuantity");
    }

    [Fact]
    public void Validate_WhenTypeInvalid_HasValidationError()
    {
        CreateProductCommand command = new("BC-001", "Widget", null, (ProductType)99, 9.99m, 50, null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Type");
    }

    [Fact]
    public void Validate_WhenPackageWithoutUnpackTargetProductId_HasValidationError()
    {
        CreateProductCommand command = new("BC-001", "Widget", null, ProductType.Package, 9.99m, 50, null,
            null, 6);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UnpackTargetProductId");
    }

    [Fact]
    public void Validate_WhenPackageWithoutUnpackQuantityPerPackage_HasValidationError()
    {
        CreateProductCommand command = new("BC-001", "Widget", null, ProductType.Package, 9.99m, 50, null,
            Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UnpackQuantityPerPackage");
    }

    [Fact]
    public void Validate_WhenBundleWithNonZeroStockQuantity_HasValidationError()
    {
        List<BundleComponentInput> components = [new(Guid.NewGuid(), 5)];
        CreateProductCommand command = new("BC-001", "Bundle", null, ProductType.Bundle, 9.99m, 5, null,
            Components: components);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StockQuantity");
    }

    [Fact]
    public void Validate_WhenBundleWithZeroStockQuantity_HasNoErrors()
    {
        List<BundleComponentInput> components = [new(Guid.NewGuid(), 5)];
        CreateProductCommand command = new("BC-001", "Bundle", null, ProductType.Bundle, 9.99m, 0, null,
            Components: components);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
