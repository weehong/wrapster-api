using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("TenantSettings");

        builder.HasKey(t => t.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(t => t.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.DefaultLowStockThreshold);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);
        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(t => t.TenantId).IsUnique();

        builder.HasMany(t => t.Recipients)
            .WithOne()
            .HasForeignKey(r => r.TenantSettingsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Recipients)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
