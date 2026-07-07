using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Events;

public sealed record PurchaseOrderReceivedEvent(
    Guid PurchaseOrderId,
    string TenantId,
    string PoNumber,
    Guid ProductId,
    string ProductName,
    string Barcode,
    int Quantity,
    DateTime OccurredOn) : IDomainEvent;
