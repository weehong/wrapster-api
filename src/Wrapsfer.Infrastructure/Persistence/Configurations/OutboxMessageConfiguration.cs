using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Infrastructure.Persistence.Outbox;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        // Id is assigned client-side; never let the database generate it.
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.OccurredOnUtc).IsRequired();

        builder.Property(m => m.Type)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(m => m.Content).IsRequired();

        builder.Property(m => m.TenantId).HasMaxLength(256);

        builder.Property(m => m.Error).HasMaxLength(4000);

        builder.Property(m => m.RetryCount).IsRequired();

        // The relay only ever scans unprocessed rows; a partial index keeps that cheap.
        builder.HasIndex(m => m.OccurredOnUtc)
            .HasDatabaseName("IX_OutboxMessages_Unprocessed")
            .HasFilter("\"ProcessedOnUtc\" IS NULL");
    }
}
