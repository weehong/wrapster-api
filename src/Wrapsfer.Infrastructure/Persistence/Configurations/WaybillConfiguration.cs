using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class WaybillConfiguration : IEntityTypeConfiguration<Waybill>
{
    public void Configure(EntityTypeBuilder<Waybill> builder)
    {
        builder.ToTable("Waybills");

        builder.HasKey(w => w.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(w => w.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(w => w.PackagingDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(w => w.WaybillNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(w => w.CancellationReason)
            .HasMaxLength(1000);

        builder.Property(w => w.PackedAt);
        builder.Property(w => w.HandedOffAt);
        builder.Property(w => w.CancelledAt);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt);
        builder.Property(w => w.CreatedBy).HasMaxLength(256);
        builder.Property(w => w.UpdatedBy).HasMaxLength(256);

        builder.HasMany(w => w.Items)
            .WithOne()
            .HasForeignKey(i => i.WaybillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(w => new { w.TenantId, w.WaybillNumber }).IsUnique();
        builder.HasIndex(w => new { w.TenantId, w.PackagingDate });
        builder.HasIndex(w => new { w.TenantId, w.Status });
        builder.HasIndex(w => new { w.TenantId, w.Status, w.CreatedAt });
    }
}
