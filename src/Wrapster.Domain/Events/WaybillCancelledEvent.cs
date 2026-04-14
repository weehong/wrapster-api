using Wrapster.Domain.Common;
using Wrapster.Domain.Enums;

namespace Wrapster.Domain.Events;

public sealed record WaybillCancelledEvent(
    Guid WaybillId,
    string TenantId,
    WaybillStatus PreviousStatus,
    string Reason,
    DateTime OccurredOn) : IDomainEvent;
