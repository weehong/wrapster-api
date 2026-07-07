using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class PartnerIntegrationCredentialConfiguration : IEntityTypeConfiguration<PartnerIntegrationCredential>
{
    public void Configure(EntityTypeBuilder<PartnerIntegrationCredential> builder)
    {
        builder.ToTable("PartnerIntegrationCredentials");

        builder.HasKey(c => c.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(c => c.TenantId)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(c => c.ClientId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.KeycloakClientUuid)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(c => c.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.IsEnabled)
            .IsRequired();

        builder.Property(c => c.LastRotatedAt);

        builder.Property(c => c.LastRotatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.DisabledAt);

        builder.Property(c => c.DisabledBy)
            .HasMaxLength(256);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(c => c.TenantId).IsUnique();
        builder.HasIndex(c => c.ClientId).IsUnique();
        builder.HasIndex(c => c.IsEnabled);
    }
}
