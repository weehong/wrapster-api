using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class FeatureEntitlementConfiguration : IEntityTypeConfiguration<FeatureEntitlement>
{
    public void Configure(EntityTypeBuilder<FeatureEntitlement> builder)
    {
        builder.ToTable("FeatureEntitlements");

        builder.HasKey(e => e.Id);

        // Activated by the webhook after creation — concurrent updates are possible.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(e => e.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Feature)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ValidFromUtc).IsRequired();
        builder.Property(e => e.ValidToUtc).IsRequired();

        builder.Property(e => e.AmountMinor).IsRequired();

        builder.Property(e => e.Currency)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(e => e.StripeCheckoutSessionId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.StripePaymentIntentId)
            .HasMaxLength(256);

        builder.Property(e => e.StripeInvoiceId)
            .HasMaxLength(256);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(e => e.StripeCheckoutSessionId).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.Feature, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.Feature, e.ValidFromUtc, e.ValidToUtc });
    }
}
