using FluentValidation;

namespace Wrapster.Application.Waybills.Commands.DeleteWaybill;

public sealed class DeleteWaybillCommandValidator : AbstractValidator<DeleteWaybillCommand>
{
    public DeleteWaybillCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
