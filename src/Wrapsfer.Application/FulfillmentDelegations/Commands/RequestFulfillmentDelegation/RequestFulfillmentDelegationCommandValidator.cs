using FluentValidation;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.RequestFulfillmentDelegation;

public sealed class RequestFulfillmentDelegationCommandValidator
    : AbstractValidator<RequestFulfillmentDelegationCommand>
{
    public RequestFulfillmentDelegationCommandValidator() =>
        RuleFor(x => x.DefaultShippingMethod).IsInEnum();
}
