namespace Wrapsfer.Application.Billing.Abstractions;

/// <summary>
/// The provider-agnostic projection of a Stripe webhook event that the billing handlers act on.
/// The infrastructure gateway parses and verifies the raw event, then maps it to this shape.
/// </summary>
public sealed record StripeWebhookNotification(
    string EventId,
    string EventType,
    string? CheckoutSessionId,
    string? PaymentIntentId,
    string? InvoiceId,
    string? CustomerId,
    IReadOnlyDictionary<string, string> Metadata);
