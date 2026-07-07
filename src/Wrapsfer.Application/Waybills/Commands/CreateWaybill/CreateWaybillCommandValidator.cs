using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.CreateWaybill;

public sealed class CreateWaybillCommandValidator : AbstractValidator<CreateWaybillCommand>
{
    public CreateWaybillCommandValidator()
    {
        RuleFor(x => x.WaybillNumber).NotEmpty().MaximumLength(100)
            .Must(n => n is null || !n.Any(char.IsWhiteSpace))
            .WithMessage("Waybill number must not contain whitespace.");
        RuleFor(x => x.PackagingDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Barcode).NotEmpty().MaximumLength(256);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
