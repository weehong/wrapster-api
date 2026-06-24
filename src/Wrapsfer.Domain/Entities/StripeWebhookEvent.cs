using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

/// <summary>
/// Records a processed Stripe webhook event keyed by its Stripe event ID. Stripe may deliver
/// the same event more than once; persisting the event ID (with a unique index) makes webhook
/// handling idempotent so a redelivery never re-applies a side effect.
/// </summary>
public sealed class StripeWebhookEvent : BaseEntity
{
    private const int StripeEventIdMaxLength = 256;
    private const int EventTypeMaxLength = 128;

    private StripeWebhookEvent()
    {
    }

    public string StripeEventId { get; private set; } = default!;
    public string EventType { get; private set; } = default!;
    public DateTime ReceivedAtUtc { get; private set; }

    public static Result<StripeWebhookEvent> Create(string stripeEventId, string eventType, DateTime receivedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(stripeEventId) || stripeEventId.Length > StripeEventIdMaxLength)
        {
            return Result<StripeWebhookEvent>.Failure(BillingErrors.InvalidStripeEventId);
        }

        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > EventTypeMaxLength)
        {
            return Result<StripeWebhookEvent>.Failure(BillingErrors.InvalidStripeEventType);
        }

        StripeWebhookEvent webhookEvent = new()
        {
            StripeEventId = stripeEventId,
            EventType = eventType,
            ReceivedAtUtc = receivedAtUtc
        };

        return Result<StripeWebhookEvent>.Success(webhookEvent);
    }
}
