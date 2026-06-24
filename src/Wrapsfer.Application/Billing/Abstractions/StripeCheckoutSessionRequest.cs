namespace Wrapsfer.Application.Billing.Abstractions;

public sealed record StripeCheckoutSessionRequest(
    string CustomerId,
    string Currency,
    int AmountMinor,
    string ProductName,
    string SuccessUrl,
    string CancelUrl,
    IReadOnlyDictionary<string, string> Metadata);
