using FluentValidation.Results;
using Wrapster.Application.Products.Commands.DeleteProduct;

namespace Wrapster.Application.Tests.Products.Validators;

public class DeleteProductCommandValidatorTests
{
    private readonly DeleteProductCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(new DeleteProductCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenIdEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(new DeleteProductCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }
}
