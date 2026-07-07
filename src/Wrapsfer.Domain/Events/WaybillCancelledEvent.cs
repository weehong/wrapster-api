using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Events;

public sealed record WaybillCancelledEvent(
    Guid WaybillId,
    string TenantId,
    WaybillStatus PreviousStatus,
    string Reason,
    DateTime OccurredOn) : IDomainEvent;
