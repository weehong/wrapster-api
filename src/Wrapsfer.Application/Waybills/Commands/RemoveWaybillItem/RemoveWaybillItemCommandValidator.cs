using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.RemoveWaybillItem;

public sealed class RemoveWaybillItemCommandValidator : AbstractValidator<RemoveWaybillItemCommand>
{
    public RemoveWaybillItemCommandValidator()
    {
        RuleFor(x => x.WaybillId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}
