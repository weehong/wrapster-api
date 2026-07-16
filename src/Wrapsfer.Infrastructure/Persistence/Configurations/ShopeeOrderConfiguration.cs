using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeOrderConfiguration : IEntityTypeConfiguration<ShopeeOrder>
{
    public void Configure(EntityTypeBuilder<ShopeeOrder> builder)
    {
        builder.ToTable("ShopeeOrders");
        builder.HasKey(o => o.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(o => o.TenantId).HasMaxLength(63).IsRequired();
        builder.Property(o => o.OrderSn).HasMaxLength(64).IsRequired();
        builder.Property(o => o.Region).HasMaxLength(16);
        builder.Property(o => o.ShopeeStatus).HasMaxLength(32).IsRequired();
        builder.Property(o => o.BuyerUsername).HasMaxLength(256);
        builder.Property(o => o.RecipientName).HasMaxLength(256);
        builder.Property(o => o.RecipientPhone).HasMaxLength(64);
        builder.Property(o => o.RecipientAddress).HasMaxLength(1024);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Property(o => o.Currency).HasMaxLength(8);
        builder.Property(o => o.CodAmount).HasPrecision(18, 2);
        builder.Property(o => o.ShippingCarrier).HasMaxLength(128);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(o => o.TrackingNumber).HasMaxLength(100);
        builder.Property(o => o.ShipmentArrangedBy).HasMaxLength(256);
        builder.Property(o => o.LabelStorageKey).HasMaxLength(512);
        builder.Property(o => o.LastShipError).HasMaxLength(512);

        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt);
        builder.Property(o => o.CreatedBy).HasMaxLength(256);
        builder.Property(o => o.UpdatedBy).HasMaxLength(256);

        builder.HasOne<Waybill>()
            .WithMany()
            .HasForeignKey(o => o.WaybillId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.ShopeeOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => new { o.TenantId, o.OrderSn }).IsUnique();
        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.ShipmentArrangedAt);
    }
}
