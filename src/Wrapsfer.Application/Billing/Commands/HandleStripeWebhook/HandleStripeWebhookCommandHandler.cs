using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Commands.HandleStripeWebhook;

internal sealed class HandleStripeWebhookCommandHandler(
    IStripeBillingGateway billingGateway,
    IStripeWebhookEventRepository webhookEventRepository,
    IFeatureEntitlementRepository entitlementRepository,
    IUnitOfWork unitOfWork,
    ILogger<HandleStripeWebhookCommandHandler> logger)
    : ICommandHandler<HandleStripeWebhookCommand>
{
    public async Task<Result> Handle(HandleStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        Result<StripeWebhookNotification> parseResult =
            billingGateway.ParseWebhookEvent(request.Payload, request.SignatureHeader);
        if (parseResult.IsFailure)
        {
            return Result.Failure(parseResult.Error);
        }

        StripeWebhookNotification notification = parseResult.Value;

        // Stripe retries deliveries; a duplicate event ID means we already applied this event. This
        // pre-check short-circuits sequential redeliveries. Two *concurrent* deliveries can both pass
        // it, but the entitlement mutation and the StripeWebhookEvent insert below are persisted in a
        // single SaveChanges (one transaction), and StripeEventId carries a unique index — so the
        // second writer's insert fails the constraint and rolls back its entitlement change too,
        // making the event apply at most once. (Activate/Cancel are also individually idempotent.)
        if (await webhookEventRepository.ExistsAsync(notification.EventId, cancellationToken))
        {
            logger.LogInformation("Ignoring duplicate Stripe webhook event {EventId} ({EventType})",
                notification.EventId, notification.EventType);
            return Result.Success();
        }

        if (notification.EventType == StripeEventTypes.CheckoutSessionCompleted
            && !string.IsNullOrWhiteSpace(notification.CheckoutSessionId))
        {
            FeatureEntitlement? entitlement = await entitlementRepository.GetByCheckoutSessionIdAsync(
                notification.CheckoutSessionId, cancellationToken);

            if (entitlement is not null)
            {
                entitlement.Activate(notification.PaymentIntentId, notification.InvoiceId);
            }
            else
            {
                logger.LogWarning(
                    "Stripe checkout.session.completed for unknown session {SessionId} (event {EventId})",
                    notification.CheckoutSessionId, notification.EventId);
            }
        }
        else if ((notification.EventType == StripeEventTypes.ChargeRefunded
                  || notification.EventType == StripeEventTypes.RefundCreated)
                 && !string.IsNullOrWhiteSpace(notification.PaymentIntentId))
        {
            FeatureEntitlement? entitlement = await entitlementRepository.GetByPaymentIntentIdAsync(
                notification.PaymentIntentId, cancellationToken);

            if (entitlement is not null)
            {
                entitlement.Cancel();
            }
            else
            {
                logger.LogWarning(
                    "Stripe refund event {EventType} for unknown payment intent {PaymentIntentId} (event {EventId})",
                    notification.EventType, notification.PaymentIntentId, notification.EventId);
            }
        }

        Result<StripeWebhookEvent> webhookEventResult = StripeWebhookEvent.Create(
            notification.EventId, notification.EventType, DateTime.UtcNow);
        if (webhookEventResult.IsFailure)
        {
            return Result.Failure(webhookEventResult.Error);
        }

        webhookEventRepository.Add(webhookEventResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
