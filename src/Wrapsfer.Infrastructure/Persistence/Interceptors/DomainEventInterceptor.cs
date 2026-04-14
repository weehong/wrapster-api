using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Infrastructure.Persistence.Interceptors;

public sealed class DomainEventInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task PublishDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        List<BaseEntity> entities = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        List<IDomainEvent> domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            Type notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            object? notification = Activator.CreateInstance(notificationType, domainEvent);

            if (notification is null)
            {
                throw new InvalidOperationException(
                    $"Failed to create notification for domain event type '{domainEvent.GetType().FullName}'. " +
                    $"Ensure '{notificationType.FullName}' has a constructor accepting the domain event instance.");
            }

            await publisher.Publish(notification, cancellationToken);
        }

        entities.ForEach(e => e.ClearDomainEvents());
    }
}
