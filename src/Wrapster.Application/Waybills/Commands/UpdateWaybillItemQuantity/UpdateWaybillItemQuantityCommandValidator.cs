using FluentValidation;

namespace Wrapster.Application.Waybills.Commands.UpdateWaybillItemQuantity;

public sealed class UpdateWaybillItemQuantityCommandValidator : AbstractValidator<UpdateWaybillItemQuantityCommand>
{
    public UpdateWaybillItemQuantityCommandValidator()
    {
        RuleFor(x => x.WaybillId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
