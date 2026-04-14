using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapster.Domain.Entities;

namespace Wrapster.Infrastructure.Persistence.Configurations;

public sealed class WaybillItemConfiguration : IEntityTypeConfiguration<WaybillItem>
{
    public void Configure(EntityTypeBuilder<WaybillItem> builder)
    {
        builder.ToTable("WaybillItems",
            t => t.HasCheckConstraint("CK_WaybillItems_Quantity_Positive", "\"Quantity\" > 0"));

        builder.HasKey(i => i.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(i => i.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.WaybillId).IsRequired();
        builder.Property(i => i.ProductId).IsRequired();

        builder.Property(i => i.ProductBarcode)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.Quantity).IsRequired();

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt);
        builder.Property(i => i.CreatedBy).HasMaxLength(256);
        builder.Property(i => i.UpdatedBy).HasMaxLength(256);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.ProductId });
        builder.HasIndex(i => new { i.WaybillId, i.ProductId }).IsUnique();
    }
}
