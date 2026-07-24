using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.AcceptFulfillmentDelegation;

public sealed record AcceptFulfillmentDelegationCommand(
    string TenantId) : ICommand<FulfillmentDelegationResponse>;
