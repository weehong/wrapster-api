using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Events;

public sealed record FulfillmentDelegationRevokedEvent(
    Guid DelegationId,
    string TenantId,
    string Reason,
    DateTime OccurredOn) : IDomainEvent;
