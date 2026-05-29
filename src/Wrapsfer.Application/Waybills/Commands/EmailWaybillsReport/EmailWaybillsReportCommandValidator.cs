using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.EmailWaybillsReport;

public sealed class EmailWaybillsReportCommandValidator : AbstractValidator<EmailWaybillsReportCommand>
{
    public EmailWaybillsReportCommandValidator()
    {
        RuleFor(x => x.Format).IsInEnum();

        RuleFor(x => x).Must(q => !q.From.HasValue || !q.To.HasValue || q.From.Value <= q.To.Value)
            .WithMessage("From date must be less than or equal to To date.");

        RuleFor(x => x.RecipientEmails)
            .NotNull()
            .Must(emails => emails is { Count: > 0 })
            .WithMessage("At least one recipient email is required.");

        RuleForEach(x => x.RecipientEmails).EmailAddress();
    }
}
