using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wrapsfer.Domain.Common;
using Wrapsfer.Infrastructure.Persistence.Outbox;

namespace Wrapsfer.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Captures domain events raised during a unit of work into the outbox <em>before</em>
/// the transaction commits, so the events persist atomically with the business data.
/// A background relay (<see cref="BackgroundServices.OutboxProcessor"/>) publishes them
/// afterwards. Dispatching here — rather than post-commit — guarantees a notification
/// failure can never roll back, lose, or fail the originating request.
/// </summary>
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            WriteOutboxMessages(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            WriteOutboxMessages(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void WriteOutboxMessages(DbContext context)
    {
        List<BaseEntity> entities = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        if (entities.Count == 0)
        {
            return;
        }

        List<OutboxMessage> messages = [];
        foreach (BaseEntity entity in entities)
        {
            foreach (IDomainEvent domainEvent in entity.DomainEvents)
            {
                Type eventType = domainEvent.GetType();
                messages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    OccurredOnUtc = DateTime.UtcNow,
                    Type = eventType.FullName!,
                    Content = JsonSerializer.Serialize(domainEvent, eventType),
                    TenantId = eventType.GetProperty("TenantId")?.GetValue(domainEvent) as string
                });
            }

            entity.ClearDomainEvents();
        }

        context.Set<OutboxMessage>().AddRange(messages);
    }
}
