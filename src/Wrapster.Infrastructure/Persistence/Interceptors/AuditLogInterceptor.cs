using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wrapster.Application.Abstractions;
using Wrapster.Domain.Common;

namespace Wrapster.Infrastructure.Persistence.Interceptors;

public sealed class AuditLogInterceptor(ITenantContext? tenantContext = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RecordAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RecordAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void RecordAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        string? userId = ResolveUserId();
        string? tenantId = ResolveTenantId();
        DateTime utcNow = DateTime.UtcNow;

        List<EntityEntry> entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (EntityEntry entry in entries)
        {
            AuditLog auditLog = new()
            {
                EntityName = entry.Entity.GetType().Name,
                EntityId = GetPrimaryKeyValue(entry),
                Action = entry.State switch
                {
                    EntityState.Added => AuditAction.Created,
                    EntityState.Modified => AuditAction.Updated,
                    EntityState.Deleted => AuditAction.Deleted,
                    _ => AuditAction.Updated
                },
                Changes = SerializeChanges(entry),
                UserId = userId,
                TenantId = tenantId,
                Timestamp = utcNow
            };

            context.Add(auditLog);
        }
    }

    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        IEnumerable<string> keyValues = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "null");

        return string.Join(",", keyValues);
    }

    private static string? SerializeChanges(EntityEntry entry)
    {
        Dictionary<string, object?> changes = new();

        switch (entry.State)
        {
            case EntityState.Added:
                foreach (PropertyEntry prop in entry.Properties.Where(p => !p.Metadata.IsPrimaryKey()))
                {
                    if (IsSensitive(prop))
                    {
                        continue;
                    }

                    changes[prop.Metadata.Name] = new { NewValue = prop.CurrentValue };
                }

                break;

            case EntityState.Modified:
                foreach (PropertyEntry prop in entry.Properties.Where(p => p.IsModified))
                {
                    if (IsSensitive(prop))
                    {
                        continue;
                    }

                    changes[prop.Metadata.Name] = new
                    {
                        OldValue = prop.OriginalValue,
                        NewValue = prop.CurrentValue
                    };
                }

                break;

            case EntityState.Deleted:
                foreach (PropertyEntry prop in entry.Properties.Where(p => !p.Metadata.IsPrimaryKey()))
                {
                    if (IsSensitive(prop))
                    {
                        continue;
                    }

                    changes[prop.Metadata.Name] = new { OldValue = prop.OriginalValue };
                }

                break;
        }

        return changes.Count > 0 ? JsonSerializer.Serialize(changes) : null;
    }

    private static bool IsSensitive(PropertyEntry prop) =>
        prop.Metadata.PropertyInfo?.GetCustomAttribute<SensitiveDataAttribute>() is not null;

    private string? ResolveUserId()
    {
        try
        {
            return tenantContext?.UserId;
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveTenantId()
    {
        try
        {
            return tenantContext?.TenantId;
        }
        catch
        {
            return null;
        }
    }
}
