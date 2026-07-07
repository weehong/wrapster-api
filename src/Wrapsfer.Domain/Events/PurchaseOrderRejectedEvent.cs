using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Events;

public sealed record PurchaseOrderRejectedEvent(
    Guid PurchaseOrderId,
    string TenantId,
    string PoNumber,
    Guid ProductId,
    string ProductName,
    string Barcode,
    int Quantity,
    string Reason,
    DateTime OccurredOn) : IDomainEvent;
