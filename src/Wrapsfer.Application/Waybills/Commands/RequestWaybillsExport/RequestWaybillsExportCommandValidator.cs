using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

public sealed class RequestWaybillsExportCommandValidator : AbstractValidator<RequestWaybillsExportCommand>
{
    public RequestWaybillsExportCommandValidator()
    {
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x).Must(q => !q.From.HasValue || !q.To.HasValue || q.From.Value <= q.To.Value)
            .WithMessage("From date must be less than or equal to To date.");

        When(x => x.RecipientEmails is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.RecipientEmails!).EmailAddress();
        });
    }
}
