using Wrapster.Domain.Common;

namespace Wrapster.Domain.Events;

public sealed record LowStockDetectedEvent(
    Guid ProductId,
    string ProductName,
    string Barcode,
    int CurrentStock,
    int Threshold,
    string TenantId,
    DateTime OccurredOn) : IDomainEvent;
