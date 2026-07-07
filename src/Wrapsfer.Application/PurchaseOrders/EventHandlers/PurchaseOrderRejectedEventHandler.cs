using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.PurchaseOrders.EventHandlers;

internal sealed class PurchaseOrderRejectedEventHandler(
    IMailer mailer,
    ITenantSettingsRepository tenantSettingsRepository,
    ILogger<PurchaseOrderRejectedEventHandler> logger)
    : IDomainEventHandler<PurchaseOrderRejectedEvent>
{
    public async Task Handle(DomainEventNotification<PurchaseOrderRejectedEvent> notification,
        CancellationToken cancellationToken)
    {
        PurchaseOrderRejectedEvent domainEvent = notification.DomainEvent;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(domainEvent.TenantId, cancellationToken);

        List<string> recipients = settings?.Recipients
            .Where(r => r.IsActive)
            .Select(r => r.Email)
            .ToList() ?? [];

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "No active recipients configured for tenant {TenantId} — skipping purchase-order-rejected notice for PO {PoNumber}",
                domainEvent.TenantId, domainEvent.PoNumber);
            return;
        }

        MailMessage mail = new()
        {
            To = recipients,
            TemplateName = "purchase-order-rejected",
            Tokens = new Dictionary<string, object?>
            {
                ["PoNumber"] = domainEvent.PoNumber,
                ["ProductName"] = domainEvent.ProductName,
                ["Barcode"] = domainEvent.Barcode,
                ["Quantity"] = domainEvent.Quantity,
                ["Reason"] = domainEvent.Reason
            }
        };

        MailRequestId requestId = await mailer.SendAsync(mail, cancellationToken);

        logger.LogInformation(
            "Enqueued purchase-order-rejected notice {MailRequestId} for tenant {TenantId} PO {PoNumber} to {RecipientCount} recipient(s)",
            requestId, domainEvent.TenantId, domainEvent.PoNumber, recipients.Count);
    }
}
