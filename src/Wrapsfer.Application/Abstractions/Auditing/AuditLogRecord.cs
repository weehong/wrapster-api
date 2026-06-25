using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.Auditing;

/// <summary>
/// Immutable description of a single application-level audit entry to be persisted by an
/// <see cref="IAuditSink"/>. Decouples the audit pipeline behavior from the persistence layer.
/// </summary>
public sealed record AuditLogRecord(
    string EntityName,
    string EntityId,
    AuditAction Action,
    string? Changes,
    string? TenantId,
    string? UserId,
    string? Username,
    DateTime Timestamp);
