using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.DeleteWaybill;

public sealed class DeleteWaybillCommandValidator : AbstractValidator<DeleteWaybillCommand>
{
    public DeleteWaybillCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
