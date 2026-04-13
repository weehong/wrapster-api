using FluentValidation.Results;
using Wrapster.Application.Products.Commands.UpdateProduct;

namespace Wrapster.Application.Tests.Products.Validators;

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
}
