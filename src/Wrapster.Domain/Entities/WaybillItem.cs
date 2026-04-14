using Wrapster.Domain.Common;
using Wrapster.Domain.Errors;

namespace Wrapster.Domain.Entities;

public sealed class WaybillItem : AuditableEntity
{
    private WaybillItem()
    {
    }

    public string TenantId { get; private set; } = default!;
    public Guid WaybillId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductBarcode { get; private set; } = default!;
    public int Quantity { get; private set; }

    internal static Result<WaybillItem> Create(
        string tenantId,
        Guid waybillId,
        Guid productId,
        string productBarcode,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<WaybillItem>.Failure(WaybillErrors.InvalidTenantId);
        }

        if (productId == Guid.Empty)
        {
            return Result<WaybillItem>.Failure(WaybillErrors.InvalidProductId);
        }

        if (string.IsNullOrWhiteSpace(productBarcode))
        {
            return Result<WaybillItem>.Failure(WaybillErrors.InvalidBarcode);
        }

        if (quantity <= 0)
        {
            return Result<WaybillItem>.Failure(WaybillErrors.InvalidQuantity);
        }

        return new WaybillItem
        {
            TenantId = tenantId,
            WaybillId = waybillId,
            ProductId = productId,
            ProductBarcode = productBarcode,
            Quantity = quantity
        };
    }

    internal Result SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure(WaybillErrors.InvalidQuantity);
        }

        Quantity = quantity;
        return Result.Success();
    }

    internal void IncrementQuantity(int delta) => Quantity += delta;
}
