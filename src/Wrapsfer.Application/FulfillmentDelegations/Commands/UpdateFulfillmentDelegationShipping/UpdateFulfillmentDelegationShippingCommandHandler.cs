using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.UpdateFulfillmentDelegationShipping;

internal sealed class UpdateFulfillmentDelegationShippingCommandHandler(
    IFulfillmentDelegationRepository delegationRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateFulfillmentDelegationShippingCommand, FulfillmentDelegationResponse>
{
    public async Task<Result<FulfillmentDelegationResponse>> Handle(
        UpdateFulfillmentDelegationShippingCommand request, CancellationToken cancellationToken)
    {
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(tenantContext.TenantId, cancellationToken);
        if (delegation is null)
        {
            return Result<FulfillmentDelegationResponse>.Failure(FulfillmentDelegationErrors.NotFound);
        }

        Result updateResult = delegation.SetDefaultShippingMethod(request.DefaultShippingMethod);
        if (updateResult.IsFailure)
        {
            return Result<FulfillmentDelegationResponse>.Failure(updateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FulfillmentDelegationResponse.FromEntity(delegation);
    }
}
