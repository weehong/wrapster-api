using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.CancelWaybill;

public sealed class CancelWaybillCommandValidator : AbstractValidator<CancelWaybillCommand>
{
    public CancelWaybillCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
