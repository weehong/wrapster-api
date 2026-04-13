using Wrapster.Domain.Common;

namespace Wrapster.Domain.Events;

public sealed record StockRecoveredEvent(
    Guid ProductId,
    string ProductName,
    string Barcode,
    int CurrentStock,
    int Threshold,
    string TenantId,
    DateTime OccurredOn) : IDomainEvent;
