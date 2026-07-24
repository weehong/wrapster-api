using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Queries.ListFulfillmentDelegations;

internal sealed class ListFulfillmentDelegationsQueryHandler(
    IFulfillmentDelegationRepository delegationRepository)
    : IQueryHandler<ListFulfillmentDelegationsQuery, IReadOnlyList<FulfillmentDelegationResponse>>
{
    public async Task<Result<IReadOnlyList<FulfillmentDelegationResponse>>> Handle(
        ListFulfillmentDelegationsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<FulfillmentDelegation> delegations =
            await delegationRepository.ListAsync(request.Status, cancellationToken);

        IReadOnlyList<FulfillmentDelegationResponse> responses = delegations
            .Select(FulfillmentDelegationResponse.FromEntity)
            .ToList();

        return Result<IReadOnlyList<FulfillmentDelegationResponse>>.Success(responses);
    }
}
