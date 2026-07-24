using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.DeclineFulfillmentDelegation;

internal sealed class DeclineFulfillmentDelegationCommandHandler(
    IFulfillmentDelegationRepository delegationRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeclineFulfillmentDelegationCommand, FulfillmentDelegationResponse>
{
    public async Task<Result<FulfillmentDelegationResponse>> Handle(
        DeclineFulfillmentDelegationCommand request, CancellationToken cancellationToken)
    {
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (delegation is null)
        {
            return Result<FulfillmentDelegationResponse>.Failure(FulfillmentDelegationErrors.NotFound);
        }

        Result declineResult = delegation.Decline(request.Reason);
        if (declineResult.IsFailure)
        {
            return Result<FulfillmentDelegationResponse>.Failure(declineResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FulfillmentDelegationResponse.FromEntity(delegation);
    }
}
