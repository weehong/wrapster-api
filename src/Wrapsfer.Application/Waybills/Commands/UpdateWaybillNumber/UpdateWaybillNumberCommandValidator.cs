using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybillNumber;

public sealed class UpdateWaybillNumberCommandValidator : AbstractValidator<UpdateWaybillNumberCommand>
{
    public UpdateWaybillNumberCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.WaybillNumber).NotEmpty().MaximumLength(100);
    }
}
