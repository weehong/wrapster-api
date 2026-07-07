using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.MarkWaybillPacked;

public sealed class MarkWaybillPackedCommandValidator : AbstractValidator<MarkWaybillPackedCommand>
{
    public MarkWaybillPackedCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
