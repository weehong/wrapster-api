using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class PartnerTenantConfiguration : IEntityTypeConfiguration<PartnerTenant>
{
    public void Configure(EntityTypeBuilder<PartnerTenant> builder)
    {
        builder.ToTable("PartnerTenants");

        builder.HasKey(p => p.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(p => p.TenantId)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(p => p.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.ContactEmail)
            .HasMaxLength(320);

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.ProvisioningStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.DeactivatedAt);

        builder.Property(p => p.LastProvisioningError)
            .HasMaxLength(1024);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(p => p.TenantId).IsUnique();
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.ProvisioningStatus);
    }
}
