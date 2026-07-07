using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.PurchaseOrders.Responses;

internal static class PurchaseOrderResponseMapper
{
    public static PurchaseOrderResponse ToResponse(PurchaseOrder purchaseOrder) =>
        new(
            purchaseOrder.Id,
            purchaseOrder.TenantId,
            purchaseOrder.PoNumber,
            purchaseOrder.ProductId,
            purchaseOrder.ProductBarcode,
            purchaseOrder.ProductName,
            purchaseOrder.Quantity,
            purchaseOrder.Status,
            purchaseOrder.RejectionReason,
            purchaseOrder.ReceivedAt,
            purchaseOrder.RejectedAt,
            purchaseOrder.CreatedAt,
            purchaseOrder.UpdatedAt,
            purchaseOrder.CreatedBy);
}
