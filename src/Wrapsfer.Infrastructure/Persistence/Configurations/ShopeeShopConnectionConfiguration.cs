using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeShopConnectionConfiguration : IEntityTypeConfiguration<ShopeeShopConnection>
{
    public void Configure(EntityTypeBuilder<ShopeeShopConnection> builder)
    {
        builder.ToTable("ShopeeShopConnections");

        builder.HasKey(c => c.Id);

        // The background refresh job and user-driven relink/unlink can race; xmin
        // detects concurrent token updates.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(c => c.TenantId)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(c => c.ShopId)
            .IsRequired();

        builder.Property(c => c.ShopName)
            .HasMaxLength(256);

        builder.Property(c => c.Region)
            .HasMaxLength(16);

        builder.Property(c => c.AccessToken)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(c => c.RefreshToken)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(c => c.AccessTokenExpiresAt).IsRequired();
        builder.Property(c => c.RefreshTokenExpiresAt).IsRequired();
        builder.Property(c => c.LinkedAt).IsRequired();

        builder.Property(c => c.LinkedBy)
            .HasMaxLength(256);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(c => c.TenantId).IsUnique();
        builder.HasIndex(c => c.AccessTokenExpiresAt);
    }
}
