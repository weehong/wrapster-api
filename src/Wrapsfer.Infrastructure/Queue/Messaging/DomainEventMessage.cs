namespace Wrapsfer.Infrastructure.Queue.Messaging;

/// <summary>
/// Transport envelope carrying a serialized domain event from the outbox relay to the
/// <see cref="BackgroundServices.DomainEventConsumer"/>. <see cref="Type"/> is the domain
/// event's CLR full name; <see cref="Content"/> is its JSON payload.
/// </summary>
public sealed record DomainEventMessage(
    Guid Id,
    string Type,
    string Content,
    string? TenantId,
    DateTime OccurredOnUtc)
{
    public const string QueueName = "wrapsfer.domain-events";
}
