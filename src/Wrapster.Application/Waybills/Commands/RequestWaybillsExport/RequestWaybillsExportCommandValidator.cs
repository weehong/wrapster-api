using FluentValidation;

namespace Wrapster.Application.Waybills.Commands.RequestWaybillsExport;

public sealed class RequestWaybillsExportCommandValidator : AbstractValidator<RequestWaybillsExportCommand>
{
    public RequestWaybillsExportCommandValidator() => RuleFor(x => x.Format).IsInEnum();
}
