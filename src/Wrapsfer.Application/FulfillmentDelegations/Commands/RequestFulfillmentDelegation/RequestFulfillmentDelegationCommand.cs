using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.RequestFulfillmentDelegation;

public sealed record RequestFulfillmentDelegationCommand(
    FulfillmentShippingMethod DefaultShippingMethod) : ICommand<FulfillmentDelegationResponse>;
