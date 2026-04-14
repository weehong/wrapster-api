using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Events;

public sealed record WaybillHandedOffEvent(
    Guid WaybillId,
    string TenantId,
    string HandedOffBy,
    DateTime OccurredOn) : IDomainEvent;
