using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Interceptors;

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
        string? username = ResolveUsername();
        string? actorName = ResolveActorName();
        string? actorRealm = ResolveActorRealm();
        string? tenantId = ResolveTenantId();
        DateTime utcNow = DateTime.UtcNow;

        List<EntityEntry> entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog
                        && e.Entity is not StockMovement
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
                Username = username,
                ActorName = actorName,
                ActorRealm = actorRealm,
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

    private string? ResolveUsername()
    {
        try
        {
            return tenantContext?.Username;
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveActorName()
    {
        try
        {
            return tenantContext?.DisplayName;
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveActorRealm()
    {
        try
        {
            return tenantContext?.ActorRealm;
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
