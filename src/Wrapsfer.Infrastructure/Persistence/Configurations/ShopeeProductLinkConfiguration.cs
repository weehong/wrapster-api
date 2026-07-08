using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeProductLinkConfiguration : IEntityTypeConfiguration<ShopeeProductLink>
{
    public void Configure(EntityTypeBuilder<ShopeeProductLink> builder)
    {
        builder.ToTable("ShopeeProductLinks");

        builder.HasKey(l => l.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(l => l.TenantId)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(l => l.ProductId)
            .IsRequired();

        builder.Property(l => l.ShopeeItemId)
            .IsRequired();

        builder.Property(l => l.ShopeeModelId)
            .IsRequired();

        builder.Property(l => l.ShopeeItemName)
            .HasMaxLength(256);

        builder.Property(l => l.ShopeeModelName)
            .HasMaxLength(256);

        builder.Property(l => l.ShopeeItemSku)
            .HasMaxLength(128);

        builder.Property(l => l.LastSyncError)
            .HasMaxLength(512);

        builder.Property(l => l.LinkedBy)
            .HasMaxLength(256);

        builder.Property(l => l.SyncFailureCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.UpdatedAt);
        builder.Property(l => l.CreatedBy).HasMaxLength(256);
        builder.Property(l => l.UpdatedBy).HasMaxLength(256);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.TenantId, l.ShopeeItemId, l.ShopeeModelId })
            .IsUnique();
        builder.HasIndex(l => new { l.TenantId, l.ProductId })
            .IsUnique();
        builder.HasIndex(l => l.NextSyncEligibleAt);
        builder.HasIndex(l => l.LastSyncAttemptedAt);
    }
}
