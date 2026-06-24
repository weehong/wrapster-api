namespace Wrapsfer.Application.Billing.Responses;

public sealed record BillingHistoryItemResponse(
    string Id,
    string Type,
    string Status,
    DateTime OccurredAtUtc,
    int AmountMinor,
    string Currency,
    string Description,
    DateOnly? AccessStartDate,
    DateOnly? AccessEndDate,
    string? HostedInvoiceUrl,
    string? InvoicePdfUrl,
    string? StripeInvoiceId,
    string? StripePaymentIntentId,
    string? StripeRefundId);
