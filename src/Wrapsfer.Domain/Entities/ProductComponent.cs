using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed class ProductComponent : AuditableEntity
{
    private ProductComponent()
    {
    }

    public string TenantId { get; private set; } = default!;
    public Guid ParentProductId { get; private set; }
    public Guid ChildProductId { get; private set; }
    public int Quantity { get; private set; }

    public Product Parent { get; private set; } = default!;
    public Product Child { get; private set; } = default!;

    public static Result<ProductComponent> Create(
        string tenantId,
        Guid parentProductId,
        Guid childProductId,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<ProductComponent>.Failure(ProductErrors.InvalidComponentTenantId);
        }

        if (parentProductId == Guid.Empty)
        {
            return Result<ProductComponent>.Failure(ProductErrors.InvalidComponentParentId);
        }

        if (childProductId == Guid.Empty)
        {
            return Result<ProductComponent>.Failure(ProductErrors.InvalidComponentChildId);
        }

        if (parentProductId == childProductId)
        {
            return Result<ProductComponent>.Failure(ProductErrors.SelfReferencingComponent);
        }

        if (quantity <= 0)
        {
            return Result<ProductComponent>.Failure(ProductErrors.InvalidComponentQuantity);
        }

        return new ProductComponent
        {
            TenantId = tenantId,
            ParentProductId = parentProductId,
            ChildProductId = childProductId,
            Quantity = quantity
        };
    }

    public Result UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
        {
            return Result.Failure(ProductErrors.InvalidComponentQuantity);
        }

        Quantity = newQuantity;
        return Result.Success();
    }
}
