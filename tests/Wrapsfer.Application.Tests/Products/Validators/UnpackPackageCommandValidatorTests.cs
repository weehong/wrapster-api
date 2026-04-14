using FluentValidation.Results;
using Wrapsfer.Application.Products.Commands.UnpackPackage;

namespace Wrapsfer.Application.Tests.Products.Validators;

public class UnpackPackageCommandValidatorTests
{
    private readonly UnpackPackageCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(new UnpackPackageCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenProductIdEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(new UnpackPackageCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProductId");
    }

    [Fact]
    public void Validate_WhenQuantityZeroOrNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(new UnpackPackageCommand(Guid.NewGuid(), 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }
}
