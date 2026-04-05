using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wrapster.Application.Abstractions;
using Wrapster.Domain.Common;

namespace Wrapster.Infrastructure.Persistence.Interceptors;

public sealed class AuditableEntityInterceptor(ITenantContext? tenantContext = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        string? userId = ResolveUserId();
        DateTime utcNow = DateTime.UtcNow;

        foreach (EntityEntry<AuditableEntity> entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.SetCreatedAt(utcNow);

                if (userId is not null)
                {
                    entry.Entity.SetCreatedBy(userId);
                }
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.SetUpdatedAt(utcNow);

                if (userId is not null)
                {
                    entry.Entity.SetUpdatedBy(userId);
                }
            }
        }
    }

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
}
