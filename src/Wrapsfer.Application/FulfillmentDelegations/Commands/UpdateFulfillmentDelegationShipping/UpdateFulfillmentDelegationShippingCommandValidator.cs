using FluentValidation;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.UpdateFulfillmentDelegationShipping;

public sealed class UpdateFulfillmentDelegationShippingCommandValidator
    : AbstractValidator<UpdateFulfillmentDelegationShippingCommand>
{
    public UpdateFulfillmentDelegationShippingCommandValidator() =>
        RuleFor(x => x.DefaultShippingMethod).IsInEnum();
}
