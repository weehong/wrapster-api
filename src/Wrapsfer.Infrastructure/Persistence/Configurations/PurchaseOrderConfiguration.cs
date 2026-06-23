using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.HasKey(p => p.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(p => p.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.PoNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ProductId)
            .IsRequired();

        builder.Property(p => p.ProductBarcode)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.ProductName)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(p => p.Quantity)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(p => p.ReceivedAt);
        builder.Property(p => p.RejectedAt);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(p => new { p.TenantId, p.PoNumber }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Status, p.CreatedAt });
    }
}
