using FluentValidation.Results;
using Wrapster.Application.Products.Commands.UpdateProductStock;

namespace Wrapster.Application.Tests.Products.Validators;

public class UpdateProductStockCommandValidatorTests
{
    private readonly UpdateProductStockCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(new UpdateProductStockCommand(Guid.NewGuid(), 50));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(new UpdateProductStockCommand(Guid.Empty, 50));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Validate_WhenNewQuantityNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(new UpdateProductStockCommand(Guid.NewGuid(), -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewQuantity");
    }
}
