using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapster.Domain.Entities;

namespace Wrapster.Infrastructure.Persistence.Configurations;

public sealed class StockAlertLogConfiguration : IEntityTypeConfiguration<StockAlertLog>
{
    public void Configure(EntityTypeBuilder<StockAlertLog> builder)
    {
        builder.ToTable("StockAlertLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.ProductId).IsRequired();

        builder.Property(l => l.ProductName)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(l => l.Barcode)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.AlertType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(l => l.StockQuantity).IsRequired();
        builder.Property(l => l.Threshold).IsRequired();

        builder.Property(l => l.RecipientsNotified).HasMaxLength(4000);

        builder.Property(l => l.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(l => l.FailureReason).HasMaxLength(512);

        builder.Property(l => l.OccurredOn).IsRequired();

        builder.HasIndex(l => new { l.TenantId, l.ProductId, l.OccurredOn });
        builder.HasIndex(l => new { l.TenantId, l.OccurredOn });
    }
}
