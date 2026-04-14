using Wrapster.Domain.Common;

namespace Wrapster.Domain.Events;

public sealed record WaybillPackedEvent(
    Guid WaybillId,
    string TenantId,
    DateTime OccurredOn) : IDomainEvent;
