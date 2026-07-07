using FluentValidation.Results;
using Wrapsfer.Application.Products.Commands.UpdateProduct;
using Wrapsfer.Application.Products.Common;

namespace Wrapsfer.Application.Tests.Products.Validators;

public class UpdateProductCommandValidatorTests
{
    private readonly UpdateProductCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        UpdateProductCommand command = new(Guid.NewGuid(), "Name", "SKU", false, 10m, 5, false);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdEmpty_HasValidationError()
    {
        UpdateProductCommand command = new(Guid.Empty, "Name", null, false, null, null, false);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Validate_WhenCostNegative_HasValidationError()
    {
        UpdateProductCommand command = new(Guid.NewGuid(), null, null, false, -1m, null, false);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Cost");
    }

    [Fact]
    public void Validate_WhenLowStockThresholdNegative_HasValidationError()
    {
        UpdateProductCommand command = new(Guid.NewGuid(), null, null, false, null, -1, false);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LowStockThreshold");
    }

    [Fact]
    public void Validate_WhenComponentsValid_HasNoErrors()
    {
        List<BundleComponentInput> components =
        [
            new(Guid.NewGuid(), 2),
            new(Guid.NewGuid(), 1)
        ];

        UpdateProductCommand command = new(Guid.NewGuid(), null, null, false, null, null, false,
            Components: components);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenComponentsEmpty_HasValidationError()
    {
        UpdateProductCommand command = new(Guid.NewGuid(), null, null, false, null, null, false,
            Components: new List<BundleComponentInput>());

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Components");
    }

    [Fact]
    public void Validate_WhenComponentsContainDuplicateChildIds_HasValidationError()
    {
        Guid duplicate = Guid.NewGuid();
        List<BundleComponentInput> components =
        [
            new(duplicate, 1),
            new(duplicate, 2)
        ];

        UpdateProductCommand command = new(Guid.NewGuid(), null, null, false, null, null, false,
            Components: components);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Components");
    }

    [Fact]
    public void Validate_WhenComponentQuantityNotPositive_HasValidationError()
    {
        List<BundleComponentInput> components =
        [
            new(Guid.NewGuid(), 0)
        ];

        UpdateProductCommand command = new(Guid.NewGuid(), null, null, false, null, null, false,
            Components: components);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Quantity"));
    }
}
