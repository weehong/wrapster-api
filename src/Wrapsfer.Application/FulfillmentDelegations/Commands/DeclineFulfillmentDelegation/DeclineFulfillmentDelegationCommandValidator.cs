using FluentValidation;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.DeclineFulfillmentDelegation;

public sealed class DeclineFulfillmentDelegationCommandValidator
    : AbstractValidator<DeclineFulfillmentDelegationCommand>
{
    public DeclineFulfillmentDelegationCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(FulfillmentDelegation.ReasonMaxLength);
    }
}
