using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class BillingErrors
{
    public static readonly Error StockReportPaymentRequired = new(
        "Billing.StockReportPaymentRequired",
        "An active stock report access pass is required. Please purchase access to continue.",
        ErrorType.PaymentRequired);

    public static readonly Error InvalidTenantId = new(
        "Billing.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidStripeCustomerId = new(
        "Billing.InvalidStripeCustomerId",
        "A Stripe customer ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidAmount = new(
        "Billing.InvalidAmount",
        "The billing amount must be greater than zero",
        ErrorType.Validation);

    public static readonly Error InvalidCurrency = new(
        "Billing.InvalidCurrency",
        "A currency is required",
        ErrorType.Validation);

    public static readonly Error InvalidValidityRange = new(
        "Billing.InvalidValidityRange",
        "The entitlement validity range is invalid",
        ErrorType.Validation);

    public static readonly Error NonUtcValidityTimestamp = new(
        "Billing.NonUtcValidityTimestamp",
        "The entitlement validity timestamps must be in UTC",
        ErrorType.Validation);

    public static readonly Error InvalidCheckoutSessionId = new(
        "Billing.InvalidCheckoutSessionId",
        "A Stripe checkout session ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidStripeEventId = new(
        "Billing.InvalidStripeEventId",
        "A Stripe event ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidStripeEventType = new(
        "Billing.InvalidStripeEventType",
        "A Stripe event type is required",
        ErrorType.Validation);

    public static readonly Error WebhookSignatureInvalid = new(
        "Billing.WebhookSignatureInvalid",
        "The Stripe webhook signature could not be verified",
        ErrorType.Validation);

    public static readonly Error CustomerProvisioningFailed = new(
        "Billing.CustomerProvisioningFailed",
        "Could not provision a billing customer. Please try again later.",
        ErrorType.Failure);

    public static readonly Error CheckoutSessionCreationFailed = new(
        "Billing.CheckoutSessionCreationFailed",
        "Could not start the checkout session. Please try again later.",
        ErrorType.Failure);

    public static readonly Error PortalSessionCreationFailed = new(
        "Billing.PortalSessionCreationFailed",
        "Could not open the billing portal. Please try again later.",
        ErrorType.Failure);

    public static readonly Error CustomerNotFound = new(
        "Billing.CustomerNotFound",
        "No billing customer exists for this tenant yet",
        ErrorType.NotFound);

    public static readonly Error StockReportAlreadyActive = new(
        "Billing.StockReportAlreadyActive",
        "Stock report access is already active for the current month",
        ErrorType.Conflict);

    public static readonly Error StockReportActiveEntitlementNotFound = new(
        "Billing.StockReportActiveEntitlementNotFound",
        "No active stock report access pass exists for this tenant",
        ErrorType.NotFound);

    public static readonly Error StockReportPaymentNotRefundable = new(
        "Billing.StockReportPaymentNotRefundable",
        "The active stock report access pass does not have a refundable Stripe payment",
        ErrorType.Conflict);
}
