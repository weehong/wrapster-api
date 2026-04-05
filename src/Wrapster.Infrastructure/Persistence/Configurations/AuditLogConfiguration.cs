using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapster.Domain.Common;

namespace Wrapster.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.Changes)
            .HasColumnType("jsonb");

        builder.Property(a => a.UserId)
            .HasMaxLength(256);

        builder.Property(a => a.TenantId)
            .HasMaxLength(256);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.HasIndex(a => a.EntityName);
        builder.HasIndex(a => a.EntityId);
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
    }
}
