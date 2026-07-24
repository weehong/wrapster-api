using FluentValidation;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.AcceptFulfillmentDelegation;

public sealed class AcceptFulfillmentDelegationCommandValidator
    : AbstractValidator<AcceptFulfillmentDelegationCommand>
{
    public AcceptFulfillmentDelegationCommandValidator() =>
        RuleFor(x => x.TenantId).NotEmpty();
}
