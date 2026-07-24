using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class FulfillmentDelegationConfiguration : IEntityTypeConfiguration<FulfillmentDelegation>
{
    public void Configure(EntityTypeBuilder<FulfillmentDelegation> builder)
    {
        builder.ToTable("FulfillmentDelegations");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.DefaultShippingMethod)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.RequestedAt).IsRequired();
        builder.Property(d => d.AcceptedAt);
        builder.Property(d => d.DeclinedAt);
        builder.Property(d => d.RevokedAt);
        builder.Property(d => d.CancelledAt);

        builder.Property(d => d.DeclineReason)
            .HasMaxLength(FulfillmentDelegation.ReasonMaxLength);

        builder.Property(d => d.RevokeReason)
            .HasMaxLength(FulfillmentDelegation.ReasonMaxLength);

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt);
        builder.Property(d => d.CreatedBy).HasMaxLength(256);
        builder.Property(d => d.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(d => d.TenantId).IsUnique();
        builder.HasIndex(d => d.Status);
    }
}
