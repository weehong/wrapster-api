using Wrapster.Domain.Common;

namespace Wrapster.Domain.Events;

public sealed record WaybillHandedOffEvent(
    Guid WaybillId,
    string TenantId,
    string HandedOffBy,
    DateTime OccurredOn) : IDomainEvent;
