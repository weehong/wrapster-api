using Microsoft.Extensions.DependencyInjection;
using Wrapsfer.Application.Abstractions.Auditing;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Infrastructure.Persistence.Auditing;

/// <summary>
/// Persists application audit records on a dedicated <see cref="ApplicationDbContext"/> resolved from
/// a fresh DI scope. Isolation is deliberate: the originating request's context may hold partial,
/// unsaved changes (e.g. a command that failed before committing), and saving them here would be
/// wrong. The fresh context's <c>AuditLogInterceptor</c> skips <see cref="AuditLog"/>, so no recursion.
/// </summary>
internal sealed class AuditSink(IServiceScopeFactory scopeFactory) : IAuditSink
{
    public async Task WriteAsync(AuditLogRecord record, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        AuditLog auditLog = new()
        {
            EntityName = record.EntityName,
            EntityId = record.EntityId,
            Action = record.Action,
            Changes = record.Changes,
            UserId = record.UserId,
            Username = record.Username,
            ActorName = record.ActorName,
            ActorRealm = record.ActorRealm,
            TenantId = record.TenantId,
            Timestamp = record.Timestamp
        };

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken);
    }
}
