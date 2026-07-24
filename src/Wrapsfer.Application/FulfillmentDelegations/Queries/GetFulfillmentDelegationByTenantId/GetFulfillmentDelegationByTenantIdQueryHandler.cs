using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Queries.GetFulfillmentDelegationByTenantId;

internal sealed class GetFulfillmentDelegationByTenantIdQueryHandler(
    IFulfillmentDelegationRepository delegationRepository)
    : IQueryHandler<GetFulfillmentDelegationByTenantIdQuery, FulfillmentDelegationResponse>
{
    public async Task<Result<FulfillmentDelegationResponse>> Handle(
        GetFulfillmentDelegationByTenantIdQuery request, CancellationToken cancellationToken)
    {
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);

        return delegation is null
            ? Result<FulfillmentDelegationResponse>.Failure(FulfillmentDelegationErrors.NotFound)
            : FulfillmentDelegationResponse.FromEntity(delegation);
    }
}
