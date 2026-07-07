using FluentValidation;

namespace Wrapsfer.Application.Products.Commands.UpdateProductStock;

public sealed class UpdateProductStockCommandValidator : AbstractValidator<UpdateProductStockCommand>
{
    public UpdateProductStockCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0);
    }
}
