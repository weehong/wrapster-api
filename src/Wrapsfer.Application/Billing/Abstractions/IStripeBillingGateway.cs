using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Billing.Abstractions;

/// <summary>
/// Boundary over the Stripe SDK. Keeps Stripe types out of the application layer so handlers
/// stay unit-testable against a mock and the SDK lives only in infrastructure.
/// </summary>
public interface IStripeBillingGateway
{
    Task<string> CreateCustomerAsync(
        string tenantId,
        string? email,
        string? displayName,
        CancellationToken cancellationToken = default);

    Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<string> CreatePortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StripeBillingHistoryItem>> ListBillingHistoryAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<string> RefundPaymentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the webhook signature and projects the event. Returns a failure with
    /// <c>BillingErrors.WebhookSignatureInvalid</c> when the signature cannot be verified.
    /// </summary>
    Result<StripeWebhookNotification> ParseWebhookEvent(string payload, string signatureHeader);
}
