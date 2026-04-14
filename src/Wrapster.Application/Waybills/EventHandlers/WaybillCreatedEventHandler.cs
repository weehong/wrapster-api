using Microsoft.Extensions.Logging;
using Wrapster.Application.Abstractions;
using Wrapster.Domain.Events;

namespace Wrapster.Application.Waybills.EventHandlers;

internal sealed class WaybillCreatedEventHandler(ILogger<WaybillCreatedEventHandler> logger)
    : IDomainEventHandler<WaybillCreatedEvent>
{
    public Task Handle(DomainEventNotification<WaybillCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        WaybillCreatedEvent domainEvent = notification.DomainEvent;
        logger.LogInformation(
            "Waybill {WaybillId} created for tenant {TenantId} — number {Number} on {Date}",
            domainEvent.WaybillId, domainEvent.TenantId, domainEvent.WaybillNumber, domainEvent.PackagingDate);
        return Task.CompletedTask;
    }
}
