using FluentValidation;

namespace Wrapsfer.Application.Waybills.Queries.CheckWaybillNumberAvailable;

public sealed class CheckWaybillNumberAvailableQueryValidator : AbstractValidator<CheckWaybillNumberAvailableQuery>
{
    public CheckWaybillNumberAvailableQueryValidator() => RuleFor(x => x.WaybillNumber).NotEmpty().MaximumLength(100);
}
