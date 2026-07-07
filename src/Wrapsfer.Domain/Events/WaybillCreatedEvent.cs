using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Events;

public sealed record WaybillCreatedEvent(
    Guid WaybillId,
    string TenantId,
    DateOnly PackagingDate,
    string WaybillNumber,
    DateTime OccurredOn) : IDomainEvent;
