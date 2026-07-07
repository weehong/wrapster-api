using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PurchaseOrders.Commands.DeletePurchaseOrder;

internal sealed class DeletePurchaseOrderCommandHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<DeletePurchaseOrderCommand>
{
    public async Task<Result> Handle(DeletePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        PurchaseOrder? purchaseOrder =
            await purchaseOrderRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (purchaseOrder is null)
        {
            return Result.Failure(PurchaseOrderErrors.NotFound);
        }

        Result deleteResult = purchaseOrder.Delete();
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
