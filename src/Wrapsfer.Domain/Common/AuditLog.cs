namespace Wrapsfer.Domain.Common;

public sealed class AuditLog
{
    public Guid Id { get; private init; } = Guid.NewGuid();
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public AuditAction Action { get; init; }
    public string? Changes { get; init; }
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public DateTime Timestamp { get; init; }
}
