using FluentValidation;

namespace Wrapsfer.Application.Waybills.Queries.CheckWaybillNumberAvailable;

public sealed class CheckWaybillNumberAvailableQueryValidator : AbstractValidator<CheckWaybillNumberAvailableQuery>
{
    public CheckWaybillNumberAvailableQueryValidator() => RuleFor(x => x.WaybillNumber)
        .NotEmpty().MaximumLength(100)
        .Must(n => n is null || !n.Any(char.IsWhiteSpace))
        .WithMessage("Waybill number must not contain whitespace.");
}
