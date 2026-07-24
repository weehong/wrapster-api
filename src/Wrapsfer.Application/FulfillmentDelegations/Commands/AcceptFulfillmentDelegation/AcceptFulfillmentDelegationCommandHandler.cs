using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.AcceptFulfillmentDelegation;

internal sealed class AcceptFulfillmentDelegationCommandHandler(
    IFulfillmentDelegationRepository delegationRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AcceptFulfillmentDelegationCommand, FulfillmentDelegationResponse>
{
    public async Task<Result<FulfillmentDelegationResponse>> Handle(
        AcceptFulfillmentDelegationCommand request, CancellationToken cancellationToken)
    {
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (delegation is null)
        {
            return Result<FulfillmentDelegationResponse>.Failure(FulfillmentDelegationErrors.NotFound);
        }

        Result acceptResult = delegation.Accept();
        if (acceptResult.IsFailure)
        {
            return Result<FulfillmentDelegationResponse>.Failure(acceptResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FulfillmentDelegationResponse.FromEntity(delegation);
    }
}
