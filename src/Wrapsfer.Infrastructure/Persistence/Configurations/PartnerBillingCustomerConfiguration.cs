using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class PartnerBillingCustomerConfiguration : IEntityTypeConfiguration<PartnerBillingCustomer>
{
    public void Configure(EntityTypeBuilder<PartnerBillingCustomer> builder)
    {
        builder.ToTable("PartnerBillingCustomers");

        builder.HasKey(c => c.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(c => c.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.StripeCustomerId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(c => c.TenantId).IsUnique();
        builder.HasIndex(c => c.StripeCustomerId).IsUnique();
    }
}
