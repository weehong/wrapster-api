using FluentValidation;

namespace Wrapster.Application.Waybills.Commands.CreateWaybill;

public sealed class CreateWaybillCommandValidator : AbstractValidator<CreateWaybillCommand>
{
    public CreateWaybillCommandValidator()
    {
        RuleFor(x => x.WaybillNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PackagingDate).NotEqual(default(DateOnly));
    }
}
