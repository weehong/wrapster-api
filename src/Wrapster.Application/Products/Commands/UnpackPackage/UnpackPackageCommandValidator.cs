using FluentValidation;

namespace Wrapster.Application.Products.Commands.UnpackPackage;

public sealed class UnpackPackageCommandValidator : AbstractValidator<UnpackPackageCommand>
{
    public UnpackPackageCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
