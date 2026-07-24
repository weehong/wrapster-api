using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.RevokeFulfillmentDelegation;

public sealed record RevokeFulfillmentDelegationCommand(
    string TenantId,
    string Reason) : ICommand<FulfillmentDelegationResponse>;
