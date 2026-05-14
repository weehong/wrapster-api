using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.CreateWaybill;

public sealed class CreateWaybillCommandValidator : AbstractValidator<CreateWaybillCommand>
{
    public CreateWaybillCommandValidator()
    {
        RuleFor(x => x.WaybillNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PackagingDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Barcode).NotEmpty().MaximumLength(256);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
