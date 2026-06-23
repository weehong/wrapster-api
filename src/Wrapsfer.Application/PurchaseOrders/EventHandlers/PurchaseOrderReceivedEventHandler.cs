using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.PurchaseOrders.EventHandlers;

internal sealed class PurchaseOrderReceivedEventHandler(
    IMailer mailer,
    ITenantSettingsRepository tenantSettingsRepository,
    ILogger<PurchaseOrderReceivedEventHandler> logger)
    : IDomainEventHandler<PurchaseOrderReceivedEvent>
{
    public async Task Handle(DomainEventNotification<PurchaseOrderReceivedEvent> notification,
        CancellationToken cancellationToken)
    {
        PurchaseOrderReceivedEvent domainEvent = notification.DomainEvent;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(domainEvent.TenantId, cancellationToken);

        List<string> recipients = settings?.Recipients
            .Where(r => r.IsActive)
            .Select(r => r.Email)
            .ToList() ?? [];

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "No active recipients configured for tenant {TenantId} — skipping purchase-order-received notice for PO {PoNumber}",
                domainEvent.TenantId, domainEvent.PoNumber);
            return;
        }

        MailMessage mail = new()
        {
            To = recipients,
            TemplateName = "purchase-order-received",
            Tokens = new Dictionary<string, object?>
            {
                ["PoNumber"] = domainEvent.PoNumber,
                ["ProductName"] = domainEvent.ProductName,
                ["Barcode"] = domainEvent.Barcode,
                ["Quantity"] = domainEvent.Quantity
            }
        };

        MailRequestId requestId = await mailer.SendAsync(mail, cancellationToken);

        logger.LogInformation(
            "Enqueued purchase-order-received notice {MailRequestId} for tenant {TenantId} PO {PoNumber} to {RecipientCount} recipient(s)",
            requestId, domainEvent.TenantId, domainEvent.PoNumber, recipients.Count);
    }
}
