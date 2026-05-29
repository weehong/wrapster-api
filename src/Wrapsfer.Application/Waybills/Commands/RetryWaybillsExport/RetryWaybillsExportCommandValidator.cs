using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.RetryWaybillsExport;

public sealed class RetryWaybillsExportCommandValidator : AbstractValidator<RetryWaybillsExportCommand>
{
    public RetryWaybillsExportCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
    }
}
