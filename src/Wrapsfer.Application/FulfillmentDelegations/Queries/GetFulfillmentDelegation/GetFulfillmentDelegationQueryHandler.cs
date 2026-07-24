using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Queries.GetFulfillmentDelegation;

internal sealed class GetFulfillmentDelegationQueryHandler(
    IFulfillmentDelegationRepository delegationRepository,
    ITenantContext tenantContext) : IQueryHandler<GetFulfillmentDelegationQuery, FulfillmentDelegationResponse>
{
    public async Task<Result<FulfillmentDelegationResponse>> Handle(
        GetFulfillmentDelegationQuery request, CancellationToken cancellationToken)
    {
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(tenantContext.TenantId, cancellationToken);

        return delegation is null
            ? Result<FulfillmentDelegationResponse>.Failure(FulfillmentDelegationErrors.NotFound)
            : FulfillmentDelegationResponse.FromEntity(delegation);
    }
}
