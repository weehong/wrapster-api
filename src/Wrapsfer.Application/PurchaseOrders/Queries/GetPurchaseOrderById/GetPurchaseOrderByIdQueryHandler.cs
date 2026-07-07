using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PurchaseOrders.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PurchaseOrders.Queries.GetPurchaseOrderById;

internal sealed class GetPurchaseOrderByIdQueryHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    ITenantContext tenantContext) : IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderResponse>
{
    public async Task<Result<PurchaseOrderResponse>> Handle(GetPurchaseOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        PurchaseOrder? purchaseOrder =
            await purchaseOrderRepository.GetByIdAsync(request.Id, tenantContext.TenantId, cancellationToken);
        if (purchaseOrder is null)
        {
            return Result<PurchaseOrderResponse>.Failure(PurchaseOrderErrors.NotFound);
        }

        return PurchaseOrderResponseMapper.ToResponse(purchaseOrder);
    }
}
