using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Events;

public sealed record StockRecoveredEvent(
    Guid ProductId,
    string ProductName,
    string Barcode,
    int CurrentStock,
    int Threshold,
    string TenantId,
    DateTime OccurredOn) : IDomainEvent;
