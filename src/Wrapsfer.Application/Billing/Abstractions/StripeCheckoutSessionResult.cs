namespace Wrapsfer.Application.Billing.Abstractions;

public sealed record StripeCheckoutSessionResult(string SessionId, string Url);
