using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class StripeWebhookEventConfiguration : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> builder)
    {
        builder.ToTable("StripeWebhookEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.StripeEventId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.ReceivedAtUtc).IsRequired();

        // Unique by Stripe event ID — the database guarantee behind idempotent webhook handling.
        builder.HasIndex(e => e.StripeEventId).IsUnique();
    }
}
