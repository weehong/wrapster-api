using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;

namespace Wrapsfer.Domain.Entities;

/// <summary>
/// A partner's request for the owner to manage its fulfillment (waybills / Shopee
/// shipment arrangement). One row per tenant, reused across the lifecycle: the owner
/// must accept a request before it becomes active (an offline contract backs the
/// arrangement), and either side can end it — the partner by cancelling, the owner by
/// revoking with a reason. Terminal states can be re-requested.
/// </summary>
public sealed class FulfillmentDelegation : AuditableEntity
{
    public const int ReasonMaxLength = 1000;

    private FulfillmentDelegation()
    {
    }

    public string TenantId { get; private set; } = default!;
    public FulfillmentDelegationStatus Status { get; private set; }
    public FulfillmentShippingMethod DefaultShippingMethod { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? DeclinedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? DeclineReason { get; private set; }
    public string? RevokeReason { get; private set; }

    public static Result<FulfillmentDelegation> Request(
        string tenantId, FulfillmentShippingMethod defaultShippingMethod)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<FulfillmentDelegation>.Failure(FulfillmentDelegationErrors.InvalidTenantId);
        }

        FulfillmentDelegation delegation = new()
        {
            TenantId = tenantId,
            Status = FulfillmentDelegationStatus.Requested,
            DefaultShippingMethod = defaultShippingMethod,
            RequestedAt = DateTime.UtcNow
        };

        delegation.AddDomainEvent(new FulfillmentDelegationRequestedEvent(
            delegation.Id, tenantId, defaultShippingMethod, DateTime.UtcNow));

        return Result<FulfillmentDelegation>.Success(delegation);
    }

    public Result ReRequest(FulfillmentShippingMethod defaultShippingMethod)
    {
        if (Status == FulfillmentDelegationStatus.Requested)
        {
            return Result.Failure(FulfillmentDelegationErrors.AlreadyRequested);
        }

        if (Status == FulfillmentDelegationStatus.Active)
        {
            return Result.Failure(FulfillmentDelegationErrors.AlreadyActive);
        }

        Status = FulfillmentDelegationStatus.Requested;
        DefaultShippingMethod = defaultShippingMethod;
        RequestedAt = DateTime.UtcNow;
        AcceptedAt = null;
        DeclinedAt = null;
        RevokedAt = null;
        CancelledAt = null;
        DeclineReason = null;
        RevokeReason = null;

        AddDomainEvent(new FulfillmentDelegationRequestedEvent(
            Id, TenantId, defaultShippingMethod, DateTime.UtcNow));

        return Result.Success();
    }

    public Result Accept()
    {
        if (Status != FulfillmentDelegationStatus.Requested)
        {
            return Result.Failure(FulfillmentDelegationErrors.NotPending);
        }

        Status = FulfillmentDelegationStatus.Active;
        AcceptedAt = DateTime.UtcNow;

        AddDomainEvent(new FulfillmentDelegationAcceptedEvent(
            Id, TenantId, DefaultShippingMethod, DateTime.UtcNow));

        return Result.Success();
    }

    public Result Decline(string reason)
    {
        if (Status != FulfillmentDelegationStatus.Requested)
        {
            return Result.Failure(FulfillmentDelegationErrors.NotPending);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(FulfillmentDelegationErrors.DeclineReasonRequired);
        }

        string trimmedReason = reason.Length > ReasonMaxLength ? reason[..ReasonMaxLength] : reason;

        Status = FulfillmentDelegationStatus.Declined;
        DeclineReason = trimmedReason;
        DeclinedAt = DateTime.UtcNow;

        AddDomainEvent(new FulfillmentDelegationDeclinedEvent(
            Id, TenantId, trimmedReason, DateTime.UtcNow));

        return Result.Success();
    }

    public Result Revoke(string reason)
    {
        if (Status != FulfillmentDelegationStatus.Active)
        {
            return Result.Failure(FulfillmentDelegationErrors.NotActive);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(FulfillmentDelegationErrors.RevokeReasonRequired);
        }

        string trimmedReason = reason.Length > ReasonMaxLength ? reason[..ReasonMaxLength] : reason;

        Status = FulfillmentDelegationStatus.Revoked;
        RevokeReason = trimmedReason;
        RevokedAt = DateTime.UtcNow;

        AddDomainEvent(new FulfillmentDelegationRevokedEvent(
            Id, TenantId, trimmedReason, DateTime.UtcNow));

        return Result.Success();
    }

    public Result Cancel()
    {
        // Idempotent: a repeated toggle-off must not overwrite the original timestamp.
        if (Status == FulfillmentDelegationStatus.Cancelled)
        {
            return Result.Success();
        }

        if (Status is not (FulfillmentDelegationStatus.Requested or FulfillmentDelegationStatus.Active))
        {
            return Result.Failure(FulfillmentDelegationErrors.InvalidStatusTransition);
        }

        Status = FulfillmentDelegationStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result SetDefaultShippingMethod(FulfillmentShippingMethod defaultShippingMethod)
    {
        if (Status is not (FulfillmentDelegationStatus.Requested or FulfillmentDelegationStatus.Active))
        {
            return Result.Failure(FulfillmentDelegationErrors.InvalidStatusTransition);
        }

        DefaultShippingMethod = defaultShippingMethod;

        return Result.Success();
    }
}
