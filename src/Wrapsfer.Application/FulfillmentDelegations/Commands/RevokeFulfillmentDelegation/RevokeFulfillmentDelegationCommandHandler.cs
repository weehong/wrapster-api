using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.RevokeFulfillmentDelegation;

internal sealed class RevokeFulfillmentDelegationCommandHandler(
    IFulfillmentDelegationRepository delegationRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeFulfillmentDelegationCommand, FulfillmentDelegationResponse>
{
    public async Task<Result<FulfillmentDelegationResponse>> Handle(
        RevokeFulfillmentDelegationCommand request, CancellationToken cancellationToken)
    {
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (delegation is null)
        {
            return Result<FulfillmentDelegationResponse>.Failure(FulfillmentDelegationErrors.NotFound);
        }

        Result revokeResult = delegation.Revoke(request.Reason);
        if (revokeResult.IsFailure)
        {
            return Result<FulfillmentDelegationResponse>.Failure(revokeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FulfillmentDelegationResponse.FromEntity(delegation);
    }
}
