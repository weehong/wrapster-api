using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeOrderItemConfiguration : IEntityTypeConfiguration<ShopeeOrderItem>
{
    public void Configure(EntityTypeBuilder<ShopeeOrderItem> builder)
    {
        builder.ToTable("ShopeeOrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId).HasMaxLength(63).IsRequired();
        builder.Property(i => i.ShopeeOrderId).IsRequired();
        builder.Property(i => i.ShopeeItemId).IsRequired();
        builder.Property(i => i.ShopeeModelId).IsRequired();
        builder.Property(i => i.ItemName).HasMaxLength(256);
        builder.Property(i => i.ModelName).HasMaxLength(256);
        builder.Property(i => i.ItemSku).HasMaxLength(128);
        builder.Property(i => i.Quantity).IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ShopeeOrderId);
        builder.HasIndex(i => new { i.TenantId, i.ShopeeItemId, i.ShopeeModelId });
    }
}
