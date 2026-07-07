using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Products.Responses;

public sealed record ProductAuditLogResponse(
    Guid Id,
    DateTime Timestamp,
    AuditAction Action,
    string EntityName,
    string EntityId,
    string? UserId,
    string? Username,
    string? ActorName,
    string? ActorRealm,
    string? TenantId,
    string? Changes)
{
    public static ProductAuditLogResponse FromAuditLog(AuditLog auditLog) =>
        new(
            auditLog.Id,
            auditLog.Timestamp,
            auditLog.Action,
            auditLog.EntityName,
            auditLog.EntityId,
            auditLog.UserId,
            auditLog.Username,
            auditLog.ActorName ?? auditLog.Username,
            auditLog.ActorRealm,
            auditLog.TenantId,
            auditLog.Changes);
}
