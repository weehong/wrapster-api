using Wrapster.Domain.Common;

namespace Wrapster.Domain.Events;

public sealed record WaybillCreatedEvent(
    Guid WaybillId,
    string TenantId,
    DateOnly PackagingDate,
    string WaybillNumber,
    DateTime OccurredOn) : IDomainEvent;
