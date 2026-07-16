using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeWebhookEventConfiguration : IEntityTypeConfiguration<ShopeeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ShopeeWebhookEvent> builder)
    {
        builder.ToTable("ShopeeWebhookEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ShopId).IsRequired();
        builder.Property(e => e.Code).IsRequired();
        builder.Property(e => e.MessageKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Payload).IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ReceivedAtUtc).IsRequired();
        builder.Property(e => e.AttemptCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Error).HasMaxLength(512);

        builder.HasIndex(e => e.MessageKey).IsUnique();
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });
        builder.HasIndex(e => e.ReceivedAtUtc);
    }
}
