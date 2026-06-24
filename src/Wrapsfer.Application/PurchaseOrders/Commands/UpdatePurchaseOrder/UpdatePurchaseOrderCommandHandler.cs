using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PurchaseOrders.Commands.UpdatePurchaseOrder;

internal sealed class UpdatePurchaseOrderCommandHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdatePurchaseOrderCommand>
{
    public async Task<Result> Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        PurchaseOrder? purchaseOrder =
            await purchaseOrderRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (purchaseOrder is null)
        {
            return Result.Failure(PurchaseOrderErrors.NotFound);
        }

        bool duplicate = await purchaseOrderRepository.ExistsByNumberAsync(
            request.PoNumber, request.Id, tenantId, cancellationToken);
        if (duplicate)
        {
            return Result.Failure(PurchaseOrderErrors.DuplicatePoNumber);
        }

        Result updateResult = purchaseOrder.Update(request.PoNumber, request.Quantity);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
