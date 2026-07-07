namespace Wrapsfer.Infrastructure.Persistence.Outbox;

/// <summary>
/// Durable record of a domain event captured in the same transaction as the business
/// data that raised it. A background relay publishes unprocessed rows to the message
/// broker, guaranteeing side effects survive process restarts and never fail the
/// originating request.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
