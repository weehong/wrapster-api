using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.MarkWaybillHandedOff;

public sealed class MarkWaybillHandedOffCommandValidator : AbstractValidator<MarkWaybillHandedOffCommand>
{
    public MarkWaybillHandedOffCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
