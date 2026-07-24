using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;

namespace Wrapsfer.Application.FulfillmentDelegations.Queries.GetFulfillmentDelegationByTenantId;

public sealed record GetFulfillmentDelegationByTenantIdQuery(
    string TenantId) : IQuery<FulfillmentDelegationResponse>;
