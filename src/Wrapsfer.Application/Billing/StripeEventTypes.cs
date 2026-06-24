namespace Wrapsfer.Application.Billing;

public static class StripeEventTypes
{
    public const string CheckoutSessionCompleted = "checkout.session.completed";
    public const string ChargeRefunded = "charge.refunded";
    public const string RefundCreated = "refund.created";
}
