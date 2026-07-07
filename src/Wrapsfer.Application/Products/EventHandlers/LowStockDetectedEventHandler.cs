using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.Services;
using Wrapsfer.Domain.Events;

namespace Wrapsfer.Application.Products.EventHandlers;

internal sealed class LowStockDetectedEventHandler(
    LowStockAlertService lowStockAlertService,
    IOptions<ProductSettings> productSettings)
    : IDomainEventHandler<LowStockDetectedEvent>
{
    public Task Handle(DomainEventNotification<LowStockDetectedEvent> notification,
        CancellationToken cancellationToken)
    {
        LowStockDetectedEvent domainEvent = notification.DomainEvent;

        LowStockAlertContext context = new(
            domainEvent.TenantId,
            domainEvent.ProductId,
            domainEvent.ProductName,
            domainEvent.Barcode,
            domainEvent.CurrentStock,
            domainEvent.Threshold);

        TimeSpan dedupeWindow = TimeSpan.FromHours(productSettings.Value.LowStockReminderIntervalHours);
        int maxAlerts = productSettings.Value.MaxLowStockAlerts;

        return lowStockAlertService.SendAsync(context, dedupeWindow, maxAlerts, cancellationToken);
    }
}
