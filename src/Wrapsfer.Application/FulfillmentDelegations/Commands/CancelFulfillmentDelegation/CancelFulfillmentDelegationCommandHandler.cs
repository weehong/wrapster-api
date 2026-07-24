using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.FulfillmentDelegations.Commands.CancelFulfillmentDelegation;

internal sealed class CancelFulfillmentDelegationCommandHandler(
    IFulfillmentDelegationRepository delegationRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelFulfillmentDelegationCommand>
{
    public async Task<Result> Handle(
        CancelFulfillmentDelegationCommand request, CancellationToken cancellationToken)
    {
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(tenantContext.TenantId, cancellationToken);
        if (delegation is null)
        {
            return Result.Failure(FulfillmentDelegationErrors.NotFound);
        }

        Result cancelResult = delegation.Cancel();
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
