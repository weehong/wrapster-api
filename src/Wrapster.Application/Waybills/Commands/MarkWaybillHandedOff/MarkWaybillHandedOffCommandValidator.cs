using FluentValidation;

namespace Wrapster.Application.Waybills.Commands.MarkWaybillHandedOff;

public sealed class MarkWaybillHandedOffCommandValidator : AbstractValidator<MarkWaybillHandedOffCommand>
{
    public MarkWaybillHandedOffCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
