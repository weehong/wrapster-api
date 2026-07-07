using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Events;

public sealed record WaybillPackedEvent(
    Guid WaybillId,
    string TenantId,
    DateTime OccurredOn) : IDomainEvent;
