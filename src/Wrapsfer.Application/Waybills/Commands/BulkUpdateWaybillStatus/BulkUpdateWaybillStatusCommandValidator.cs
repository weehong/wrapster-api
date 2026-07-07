using FluentValidation;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Commands.BulkUpdateWaybillStatus;

public sealed class BulkUpdateWaybillStatusCommandValidator : AbstractValidator<BulkUpdateWaybillStatusCommand>
{
    private const int MaxIds = 100;

    public BulkUpdateWaybillStatusCommandValidator()
    {
        RuleFor(x => x.Ids)
            .NotEmpty().WithMessage("At least one waybill ID is required.")
            .Must(ids => ids is null || ids.Count <= MaxIds)
            .WithMessage($"A maximum of {MaxIds} waybill IDs can be updated per request.");

        RuleForEach(x => x.Ids)
            .NotEmpty().WithMessage("Waybill IDs must not be empty.");

        RuleFor(x => x.Status)
            .Must(status => status is WaybillStatus.Packed or WaybillStatus.HandedOff or WaybillStatus.Cancelled)
            .WithMessage("Status must be Packed, HandedOff, or Cancelled.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(1000)
            .When(x => x.Status == WaybillStatus.Cancelled);
    }
}
