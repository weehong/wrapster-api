using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.DeclineFulfillmentDelegation;

public sealed record DeclineFulfillmentDelegationCommand(
    string TenantId,
    string Reason) : ICommand<FulfillmentDelegationResponse>;
