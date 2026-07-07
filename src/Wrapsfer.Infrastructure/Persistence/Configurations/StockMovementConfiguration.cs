using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.ProductId)
            .IsRequired();

        builder.Property(m => m.MovementType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.QuantityDelta)
            .IsRequired();

        builder.Property(m => m.QuantityAfter)
            .IsRequired();

        builder.Property(m => m.UnitCost)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasMaxLength(256);

        // Point-in-time queries select the latest movement per product before a cutoff.
        builder.HasIndex(m => new { m.TenantId, m.ProductId, m.OccurredAt });
        builder.HasIndex(m => new { m.TenantId, m.OccurredAt });
    }
}
