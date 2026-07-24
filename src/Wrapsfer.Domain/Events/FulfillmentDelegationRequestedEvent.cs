using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Events;

public sealed record FulfillmentDelegationRequestedEvent(
    Guid DelegationId,
    string TenantId,
    FulfillmentShippingMethod DefaultShippingMethod,
    DateTime OccurredOn) : IDomainEvent;
