using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybill;

public sealed class UpdateWaybillCommandValidator : AbstractValidator<UpdateWaybillCommand>
{
    public UpdateWaybillCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.WaybillNumber).NotEmpty().MaximumLength(100)
            .Must(n => n is null || !n.Any(char.IsWhiteSpace))
            .WithMessage("Waybill number must not contain whitespace.");
        RuleFor(x => x.PackagingDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Items).NotNull();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.Barcode).MaximumLength(256);
            item.RuleFor(i => i).Must(i => i.ProductId.HasValue || !string.IsNullOrWhiteSpace(i.Barcode))
                .WithMessage("Each item must include a ProductId or Barcode.");
        });
    }
}
