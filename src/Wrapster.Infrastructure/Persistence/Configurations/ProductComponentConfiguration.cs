using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapster.Domain.Entities;

namespace Wrapster.Infrastructure.Persistence.Configurations;

public sealed class ProductComponentConfiguration : IEntityTypeConfiguration<ProductComponent>
{
    public void Configure(EntityTypeBuilder<ProductComponent> builder)
    {
        builder.ToTable("ProductComponents");

        builder.HasKey(pc => pc.Id);

        builder.Property(pc => pc.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(pc => pc.ParentProductId)
            .IsRequired();

        builder.Property(pc => pc.ChildProductId)
            .IsRequired();

        builder.Property(pc => pc.Quantity)
            .IsRequired();

        builder.Property(pc => pc.CreatedAt).IsRequired();
        builder.Property(pc => pc.UpdatedAt);
        builder.Property(pc => pc.CreatedBy).HasMaxLength(256);
        builder.Property(pc => pc.UpdatedBy).HasMaxLength(256);

        builder.HasOne(pc => pc.Parent)
            .WithMany(p => p.Components)
            .HasForeignKey(pc => pc.ParentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pc => pc.Child)
            .WithMany()
            .HasForeignKey(pc => pc.ChildProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pc => new { pc.TenantId, pc.ParentProductId, pc.ChildProductId }).IsUnique();
        builder.HasIndex(pc => pc.ParentProductId);
        builder.HasIndex(pc => pc.ChildProductId);
    }
}
