using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.RequestFulfillmentDelegation;

internal sealed class RequestFulfillmentDelegationCommandHandler(
    IFulfillmentDelegationRepository delegationRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestFulfillmentDelegationCommand, FulfillmentDelegationResponse>
{
    public async Task<Result<FulfillmentDelegationResponse>> Handle(
        RequestFulfillmentDelegationCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        FulfillmentDelegation? existing =
            await delegationRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        FulfillmentDelegation delegation;
        if (existing is null)
        {
            Result<FulfillmentDelegation> createResult =
                FulfillmentDelegation.Request(tenantId, request.DefaultShippingMethod);
            if (createResult.IsFailure)
            {
                return Result<FulfillmentDelegationResponse>.Failure(createResult.Error);
            }

            delegation = createResult.Value;
            delegationRepository.Add(delegation);
        }
        else
        {
            Result reRequestResult = existing.ReRequest(request.DefaultShippingMethod);
            if (reRequestResult.IsFailure)
            {
                return Result<FulfillmentDelegationResponse>.Failure(reRequestResult.Error);
            }

            delegation = existing;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FulfillmentDelegationResponse.FromEntity(delegation);
    }
}
