using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Infrastructure.Persistence.Outbox;

namespace Wrapsfer.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PartnerTenant> PartnerTenants => Set<PartnerTenant>();
    public DbSet<PartnerIntegrationCredential> PartnerIntegrationCredentials => Set<PartnerIntegrationCredential>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductComponent> ProductComponents => Set<ProductComponent>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<StockAlertLog> StockAlertLogs => Set<StockAlertLog>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Waybill> Waybills => Set<Waybill>();
    public DbSet<WaybillItem> WaybillItems => Set<WaybillItem>();
    public DbSet<WaybillExportJob> WaybillExportJobs => Set<WaybillExportJob>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PartnerBillingCustomer> PartnerBillingCustomers => Set<PartnerBillingCustomer>();
    public DbSet<FeatureEntitlement> FeatureEntitlements => Set<FeatureEntitlement>();
    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();
    public DbSet<ShopeeShopConnection> ShopeeShopConnections => Set<ShopeeShopConnection>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // BaseEntity.Id is initialised client-side via Guid.NewGuid(). Without this,
        // EF treats a new child added to a tracked parent's collection as Modified
        // (because the PK is non-sentinel) and emits an UPDATE instead of INSERT.
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            IMutableProperty? idProperty = entityType.FindProperty(nameof(BaseEntity.Id));
            if (idProperty is not null)
            {
                idProperty.ValueGenerated = ValueGenerated.Never;
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
