using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.PurchaseOrders.Responses;

public sealed record PurchaseOrderResponse(
    Guid Id,
    string TenantId,
    string PoNumber,
    Guid ProductId,
    string ProductBarcode,
    string ProductName,
    int Quantity,
    PurchaseOrderStatus Status,
    string? RejectionReason,
    DateTime? ReceivedAt,
    DateTime? RejectedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? CreatedBy);
