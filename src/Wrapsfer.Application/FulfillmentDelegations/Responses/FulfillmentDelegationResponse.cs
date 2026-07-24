using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.FulfillmentDelegations.Responses;

public sealed record FulfillmentDelegationResponse(
    string TenantId,
    FulfillmentDelegationStatus Status,
    FulfillmentShippingMethod DefaultShippingMethod,
    DateTime RequestedAt,
    DateTime? AcceptedAt,
    DateTime? DeclinedAt,
    DateTime? RevokedAt,
    DateTime? CancelledAt,
    string? DeclineReason,
    string? RevokeReason)
{
    public static FulfillmentDelegationResponse FromEntity(FulfillmentDelegation entity) => new(
        entity.TenantId,
        entity.Status,
        entity.DefaultShippingMethod,
        entity.RequestedAt,
        entity.AcceptedAt,
        entity.DeclinedAt,
        entity.RevokedAt,
        entity.CancelledAt,
        entity.DeclineReason,
        entity.RevokeReason);
}
