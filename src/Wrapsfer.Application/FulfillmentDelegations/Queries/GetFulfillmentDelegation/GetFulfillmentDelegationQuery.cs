using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;

namespace Wrapsfer.Application.FulfillmentDelegations.Queries.GetFulfillmentDelegation;

public sealed record GetFulfillmentDelegationQuery : IQuery<FulfillmentDelegationResponse>;
