using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PurchaseOrders.Commands.RejectPurchaseOrder;

internal sealed class RejectPurchaseOrderCommandHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<RejectPurchaseOrderCommand>
{
    public async Task<Result> Handle(RejectPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        PurchaseOrder? purchaseOrder =
            await purchaseOrderRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (purchaseOrder is null)
        {
            return Result.Failure(PurchaseOrderErrors.NotFound);
        }

        Result rejectResult = purchaseOrder.Reject(request.Reason);
        if (rejectResult.IsFailure)
        {
            return rejectResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
