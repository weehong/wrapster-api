using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class ProductComponentTests
{
    [Fact]
    public void Create_WithValidInput_ReturnsSuccess()
    {
        Guid parentId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();

        Result<ProductComponent> result = ProductComponent.Create("tenant-1", parentId, childId, 5);

        result.IsSuccess.Should().BeTrue();
        ProductComponent component = result.Value;
        component.TenantId.Should().Be("tenant-1");
        component.ParentProductId.Should().Be(parentId);
        component.ChildProductId.Should().Be(childId);
        component.Quantity.Should().Be(5);
        component.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithEmptyTenantId_ReturnsFailure()
    {
        Result<ProductComponent> result = ProductComponent.Create("", Guid.NewGuid(), Guid.NewGuid(), 5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentTenantId.Code);
    }

    [Fact]
    public void Create_WithEmptyParentId_ReturnsFailure()
    {
        Result<ProductComponent> result = ProductComponent.Create("tenant", Guid.Empty, Guid.NewGuid(), 5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentParentId.Code);
    }

    [Fact]
    public void Create_WithEmptyChildId_ReturnsFailure()
    {
        Result<ProductComponent> result = ProductComponent.Create("tenant", Guid.NewGuid(), Guid.Empty, 5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentChildId.Code);
    }

    [Fact]
    public void Create_WithSelfReference_ReturnsFailure()
    {
        Guid id = Guid.NewGuid();
        Result<ProductComponent> result = ProductComponent.Create("tenant", id, id, 5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.SelfReferencingComponent.Code);
    }

    [Fact]
    public void Create_WithZeroQuantity_ReturnsFailure()
    {
        Result<ProductComponent> result = ProductComponent.Create("tenant", Guid.NewGuid(), Guid.NewGuid(), 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentQuantity.Code);
    }

    [Fact]
    public void Create_WithNegativeQuantity_ReturnsFailure()
    {
        Result<ProductComponent> result = ProductComponent.Create("tenant", Guid.NewGuid(), Guid.NewGuid(), -1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentQuantity.Code);
    }

    [Fact]
    public void UpdateQuantity_WithValidValue_UpdatesAndReturnsSuccess()
    {
        Result<ProductComponent> createResult = ProductComponent.Create("tenant", Guid.NewGuid(), Guid.NewGuid(), 3);
        ProductComponent component = createResult.Value;

        Result result = component.UpdateQuantity(10);

        result.IsSuccess.Should().BeTrue();
        component.Quantity.Should().Be(10);
    }

    [Fact]
    public void UpdateQuantity_WithZero_ReturnsFailure()
    {
        Result<ProductComponent> createResult = ProductComponent.Create("tenant", Guid.NewGuid(), Guid.NewGuid(), 3);
        ProductComponent component = createResult.Value;

        Result result = component.UpdateQuantity(0);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentQuantity.Code);
    }
}
