using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapster.Domain.Entities;

namespace Wrapster.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(p => p.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Barcode)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.SkuCode)
            .HasMaxLength(256);

        builder.Property(p => p.Name)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.Cost)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.StockQuantity)
            .IsRequired();

        builder.Property(p => p.ReservedQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Products_ReservedQuantity_NonNegative",
            "\"ReservedQuantity\" >= 0"));

        builder.Ignore(p => p.AvailableQuantity);

        builder.Property(p => p.LowStockThreshold);

        builder.Property(p => p.UnpackTargetProductId);

        builder.Property(p => p.UnpackQuantityPerPackage);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(p => p.UnpackTargetProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Components)
            .WithOne(pc => pc.Parent)
            .HasForeignKey(pc => pc.ParentProductId);

        builder.Navigation(p => p.Components)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(p => new { p.TenantId, p.Barcode }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.SkuCode });
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder.HasIndex(p => p.UnpackTargetProductId);
    }
}
