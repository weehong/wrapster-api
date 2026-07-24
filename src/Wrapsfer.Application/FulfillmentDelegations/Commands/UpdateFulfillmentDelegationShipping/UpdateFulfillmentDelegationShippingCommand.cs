using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.UpdateFulfillmentDelegationShipping;

public sealed record UpdateFulfillmentDelegationShippingCommand(
    FulfillmentShippingMethod DefaultShippingMethod) : ICommand<FulfillmentDelegationResponse>;
