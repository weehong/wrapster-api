using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.FulfillmentDelegations.Queries.ListFulfillmentDelegations;

public sealed record ListFulfillmentDelegationsQuery(
    FulfillmentDelegationStatus? Status) : IQuery<IReadOnlyList<FulfillmentDelegationResponse>>;
