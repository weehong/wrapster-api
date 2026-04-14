using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Tests.Helpers;

namespace Wrapsfer.Domain.Tests.Common;

public class AuditableEntityTests
{
    [Fact]
    public void SetCreatedAt_SetsValue()
    {
        Product product = ProductFactory.CreateSingle();
        DateTime now = DateTime.UtcNow;

        product.SetCreatedAt(now);

        product.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void SetUpdatedAt_SetsValue()
    {
        Product product = ProductFactory.CreateSingle();
        DateTime now = DateTime.UtcNow;

        product.SetUpdatedAt(now);

        product.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void SetCreatedBy_SetsValue()
    {
        Product product = ProductFactory.CreateSingle();

        product.SetCreatedBy("user-1");

        product.CreatedBy.Should().Be("user-1");
    }

    [Fact]
    public void SetUpdatedBy_SetsValue()
    {
        Product product = ProductFactory.CreateSingle();

        product.SetUpdatedBy("user-2");

        product.UpdatedBy.Should().Be("user-2");
    }
}
