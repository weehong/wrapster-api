using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductComponent> ProductComponents => Set<ProductComponent>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<StockAlertLog> StockAlertLogs => Set<StockAlertLog>();
    public DbSet<Waybill> Waybills => Set<Waybill>();
    public DbSet<WaybillItem> WaybillItems => Set<WaybillItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
