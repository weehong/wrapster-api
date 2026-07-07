using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.AddWaybillItem;

public sealed class AddWaybillItemCommandValidator : AbstractValidator<AddWaybillItemCommand>
{
    public AddWaybillItemCommandValidator()
    {
        RuleFor(x => x.WaybillId).NotEmpty();
        RuleFor(x => x.Barcode).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
