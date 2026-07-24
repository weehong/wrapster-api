using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class FulfillmentDelegationErrors
{
    public static readonly Error NotFound = new(
        "FulfillmentDelegation.NotFound",
        "No fulfillment delegation exists for this tenant",
        ErrorType.NotFound);

    public static readonly Error InvalidTenantId = new(
        "FulfillmentDelegation.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error AlreadyRequested = new(
        "FulfillmentDelegation.AlreadyRequested",
        "A fulfillment delegation request is already pending",
        ErrorType.Conflict);

    public static readonly Error AlreadyActive = new(
        "FulfillmentDelegation.AlreadyActive",
        "The fulfillment delegation is already active",
        ErrorType.Conflict);

    public static readonly Error NotPending = new(
        "FulfillmentDelegation.NotPending",
        "Only a pending request can be accepted or declined",
        ErrorType.Conflict);

    public static readonly Error NotActive = new(
        "FulfillmentDelegation.NotActive",
        "Only an active delegation can be revoked",
        ErrorType.Conflict);

    public static readonly Error InvalidStatusTransition = new(
        "FulfillmentDelegation.InvalidStatusTransition",
        "The requested action is not allowed from the current status",
        ErrorType.Conflict);

    public static readonly Error DeclineReasonRequired = new(
        "FulfillmentDelegation.DeclineReasonRequired",
        "A reason is required when declining a fulfillment delegation request",
        ErrorType.Validation);

    public static readonly Error RevokeReasonRequired = new(
        "FulfillmentDelegation.RevokeReasonRequired",
        "A reason is required when revoking a fulfillment delegation",
        ErrorType.Validation);
}
