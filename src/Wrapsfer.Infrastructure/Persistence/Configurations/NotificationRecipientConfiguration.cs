using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("NotificationRecipients");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.TenantSettingsId).IsRequired();

        builder.Property(r => r.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(r => r.IsActive).IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);
        builder.Property(r => r.CreatedBy).HasMaxLength(256);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(r => new { r.TenantId, r.Email }).IsUnique();
        builder.HasIndex(r => r.TenantSettingsId);
    }
}
