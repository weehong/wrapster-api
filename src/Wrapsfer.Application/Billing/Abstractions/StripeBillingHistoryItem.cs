namespace Wrapsfer.Application.Billing.Abstractions;

public sealed record StripeBillingHistoryItem(
    string Id,
    string Type,
    string Status,
    DateTime OccurredAtUtc,
    int AmountMinor,
    string Currency,
    string Description,
    string? HostedInvoiceUrl,
    string? InvoicePdfUrl,
    string? StripeInvoiceId,
    string? StripePaymentIntentId,
    string? StripeRefundId);
