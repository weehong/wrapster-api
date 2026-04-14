using Wrapster.Domain.Common;

namespace Wrapster.Domain.Events;

public sealed record WaybillItemAddedEvent(
    Guid WaybillId,
    Guid ItemId,
    Guid ProductId,
    string ProductBarcode,
    int Quantity,
    string TenantId,
    DateTime OccurredOn) : IDomainEvent;
