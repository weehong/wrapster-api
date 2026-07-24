using FluentValidation;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.RevokeFulfillmentDelegation;

public sealed class RevokeFulfillmentDelegationCommandValidator
    : AbstractValidator<RevokeFulfillmentDelegationCommand>
{
    public RevokeFulfillmentDelegationCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(FulfillmentDelegation.ReasonMaxLength);
    }
}
