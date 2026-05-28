using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class WaybillExportJobConfiguration : IEntityTypeConfiguration<WaybillExportJob>
{
    public void Configure(EntityTypeBuilder<WaybillExportJob> builder)
    {
        builder.ToTable("WaybillExportJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.RequestedByUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(j => j.RequesterTenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(j => j.Format)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(j => j.PartnerTenantIdsJson)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(j => j.RecipientEmailsJson)
            .HasMaxLength(8000);

        builder.Property(j => j.FailureReason)
            .HasMaxLength(512);

        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.CompletedAt);

        builder.HasIndex(j => new { j.RequestedByUserId, j.CreatedAt });
    }
}
