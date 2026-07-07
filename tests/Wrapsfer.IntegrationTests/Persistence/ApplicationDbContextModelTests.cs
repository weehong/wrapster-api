using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wrapsfer.Domain.Common;
using Wrapsfer.Infrastructure.Persistence;

namespace Wrapsfer.IntegrationTests.Persistence;

public class ApplicationDbContextModelTests
{
    [Fact]
    public void AllBaseEntityIds_AreValueGeneratedNever()
    {
        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=stub;Database=stub;Username=stub;Password=stub")
                .Options;

        using ApplicationDbContext context = new(options);
        IModel model = context.Model;

        IEnumerable<IEntityType> baseEntityTypes = model.GetEntityTypes()
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType));

        baseEntityTypes.Should().NotBeEmpty("at least one BaseEntity-derived entity should be mapped");

        foreach (IEntityType entityType in baseEntityTypes)
        {
            IProperty? id = entityType.FindProperty(nameof(BaseEntity.Id));
            id.Should().NotBeNull($"{entityType.ClrType.Name} should map BaseEntity.Id");
            id!.ValueGenerated.Should().Be(
                ValueGenerated.Never,
                $"{entityType.ClrType.Name}.Id must be ValueGenerated.Never so EF treats children added via tracked collections as Added, not Modified");
        }
    }
}
