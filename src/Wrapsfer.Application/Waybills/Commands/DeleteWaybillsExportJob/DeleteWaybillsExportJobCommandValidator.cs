using FluentValidation;

namespace Wrapsfer.Application.Waybills.Commands.DeleteWaybillsExportJob;

public sealed class DeleteWaybillsExportJobCommandValidator
    : AbstractValidator<DeleteWaybillsExportJobCommand>
{
    public DeleteWaybillsExportJobCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
    }
}
