using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;

namespace Wrapsfer.Domain.Entities;

public sealed class Product : AuditableEntity
{
    private readonly List<ProductComponent> _components = [];

    private Product()
    {
    }

    public string TenantId { get; private set; } = default!;
    public string Barcode { get; private set; } = default!;
    public string? SkuCode { get; private set; }
    public string Name { get; private set; } = default!;
    public ProductType Type { get; private set; }
    public decimal Cost { get; private set; }
    public int StockQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int AvailableQuantity => StockQuantity - ReservedQuantity;
    public int? LowStockThreshold { get; private set; }
    public Guid? UnpackTargetProductId { get; private set; }
    public int? UnpackQuantityPerPackage { get; private set; }

    public IReadOnlyCollection<ProductComponent> Components => _components.AsReadOnly();

    public static Result<Product> Create(
        string tenantId,
        string barcode,
        string name,
        ProductType type,
        decimal cost,
        int stockQuantity,
        string? skuCode = null,
        int? lowStockThreshold = null,
        Guid? unpackTargetProductId = null,
        int? unpackQuantityPerPackage = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<Product>.Failure(ProductErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Result<Product>.Failure(ProductErrors.InvalidBarcode);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Product>.Failure(ProductErrors.InvalidName);
        }

        if (cost < 0)
        {
            return Result<Product>.Failure(ProductErrors.InvalidCost);
        }

        if (stockQuantity < 0)
        {
            return Result<Product>.Failure(ProductErrors.InvalidStockQuantity);
        }

        if (type == ProductType.Bundle && stockQuantity != 0)
        {
            return Result<Product>.Failure(ProductErrors.CannotSetBundleStock);
        }

        if (type == ProductType.Package)
        {
            if (!unpackTargetProductId.HasValue || !unpackQuantityPerPackage.HasValue)
            {
                return Result<Product>.Failure(ProductErrors.MissingUnpackTarget);
            }
        }

        Product product = new()
        {
            TenantId = tenantId,
            Barcode = barcode,
            Name = name,
            Type = type,
            Cost = cost,
            StockQuantity = stockQuantity,
            SkuCode = skuCode,
            LowStockThreshold = lowStockThreshold,
            UnpackTargetProductId = unpackTargetProductId,
            UnpackQuantityPerPackage = unpackQuantityPerPackage
        };

        if (unpackTargetProductId.HasValue && unpackTargetProductId.Value == product.Id)
        {
            return Result<Product>.Failure(ProductErrors.SelfReferencingUnpackTarget);
        }

        return Result<Product>.Success(product);
    }

    public Result DeductStock(int amount)
    {
        if (Type == ProductType.Bundle)
        {
            return Result.Failure(ProductErrors.CannotDeductBundleStock);
        }

        if (StockQuantity < amount)
        {
            return Result.Failure(ProductErrors.InsufficientStock);
        }

        StockQuantity -= amount;

        return Result.Success();
    }

    public Result<int> Unpack(int quantity)
    {
        if (Type != ProductType.Package)
        {
            return Result<int>.Failure(ProductErrors.NotAPackage);
        }

        if (StockQuantity < quantity)
        {
            return Result<int>.Failure(ProductErrors.InsufficientStock);
        }

        StockQuantity -= quantity;

        return Result<int>.Success(quantity * UnpackQuantityPerPackage!.Value);
    }

    public static int ComputeBundleQuantity(IReadOnlyList<(int stock, int ratio)> componentData)
    {
        if (componentData.Count == 0)
        {
            return 0;
        }

        int min = int.MaxValue;

        foreach ((int stock, int ratio) in componentData)
        {
            if (ratio <= 0)
            {
                return 0;
            }

            int available = stock / ratio;

            if (available < min)
            {
                min = available;
            }
        }

        return min;
    }

    public Result RestoreStock(int amount, int fallbackThreshold = 0)
    {
        if (amount <= 0)
        {
            return Result.Failure(ProductErrors.InvalidRestoreAmount);
        }

        if (Type == ProductType.Bundle)
        {
            return Result.Failure(ProductErrors.CannotSetBundleStock);
        }

        int previousQuantity = StockQuantity;
        StockQuantity += amount;

        int effectiveThreshold = GetEffectiveThreshold(fallbackThreshold);
        if (previousQuantity < effectiveThreshold && StockQuantity >= effectiveThreshold)
        {
            RaiseStockRecoveredEvent(fallbackThreshold);
        }

        return Result.Success();
    }

    public Result CanReserve(int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure(ProductErrors.InvalidReservationQuantity);
        }

        if (Type == ProductType.Bundle)
        {
            return Result.Failure(ProductErrors.CannotReserveBundleStock);
        }

        if (AvailableQuantity < quantity)
        {
            return Result.Failure(ProductErrors.InsufficientStock);
        }

        return Result.Success();
    }

    public Result CanRelease(int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure(ProductErrors.InvalidReservationQuantity);
        }

        if (ReservedQuantity < quantity)
        {
            return Result.Failure(ProductErrors.ReservationMismatch);
        }

        return Result.Success();
    }

    public Result CanConsume(int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure(ProductErrors.InvalidReservationQuantity);
        }

        if (Type == ProductType.Bundle)
        {
            return Result.Failure(ProductErrors.CannotDeductBundleStock);
        }

        if (ReservedQuantity < quantity)
        {
            return Result.Failure(ProductErrors.ReservationMismatch);
        }

        if (StockQuantity < quantity)
        {
            return Result.Failure(ProductErrors.InsufficientStock);
        }

        return Result.Success();
    }

    public Result Reserve(int quantity)
    {
        Result canReserve = CanReserve(quantity);
        if (canReserve.IsFailure)
        {
            return canReserve;
        }

        ReservedQuantity += quantity;
        return Result.Success();
    }

    public Result Release(int quantity)
    {
        Result canRelease = CanRelease(quantity);
        if (canRelease.IsFailure)
        {
            return canRelease;
        }

        ReservedQuantity -= quantity;
        return Result.Success();
    }

    public Result Consume(int quantity, int fallbackThreshold = 0)
    {
        Result canConsume = CanConsume(quantity);
        if (canConsume.IsFailure)
        {
            return canConsume;
        }

        int previousQuantity = StockQuantity;
        StockQuantity -= quantity;
        ReservedQuantity -= quantity;

        int effectiveThreshold = GetEffectiveThreshold(fallbackThreshold);
        if (previousQuantity >= effectiveThreshold && StockQuantity < effectiveThreshold)
        {
            RaiseLowStockEvent(fallbackThreshold);
        }

        return Result.Success();
    }

    public Result SetStockQuantity(int quantity, int fallbackThreshold = 0)
    {
        if (quantity < 0)
        {
            return Result.Failure(ProductErrors.InvalidStockQuantity);
        }

        if (Type == ProductType.Bundle)
        {
            return Result.Failure(ProductErrors.CannotSetBundleStock);
        }

        int previousQuantity = StockQuantity;
        StockQuantity = quantity;

        int effectiveThreshold = GetEffectiveThreshold(fallbackThreshold);

        if (previousQuantity >= effectiveThreshold && StockQuantity < effectiveThreshold)
        {
            RaiseLowStockEvent(fallbackThreshold);
        }
        else if (previousQuantity < effectiveThreshold && StockQuantity >= effectiveThreshold)
        {
            RaiseStockRecoveredEvent(fallbackThreshold);
        }

        return Result.Success();
    }

    public void Update(string? name, string? skuCode, bool clearSkuCode, decimal? cost, int? lowStockThreshold,
        bool clearLowStockThreshold, int fallbackThreshold = 0)
    {
        if (name is not null)
        {
            Name = name;
        }

        if (clearSkuCode)
        {
            SkuCode = null;
        }
        else if (skuCode is not null)
        {
            SkuCode = skuCode;
        }

        if (cost.HasValue)
        {
            Cost = cost.Value;
        }

        bool thresholdChanged = false;
        if (clearLowStockThreshold)
        {
            thresholdChanged = LowStockThreshold.HasValue;
            LowStockThreshold = null;
        }
        else if (lowStockThreshold.HasValue)
        {
            thresholdChanged = LowStockThreshold != lowStockThreshold.Value;
            LowStockThreshold = lowStockThreshold.Value;
        }

        if (thresholdChanged && Type != ProductType.Bundle)
        {
            CheckLowStock(fallbackThreshold);
        }
    }

    public Result UpdateUnpackConfig(Guid targetProductId, int quantityPerPackage)
    {
        if (Type != ProductType.Package)
        {
            return Result.Failure(ProductErrors.NotAPackageForUnpackConfig);
        }

        if (targetProductId == Id)
        {
            return Result.Failure(ProductErrors.SelfReferencingUnpackTarget);
        }

        UnpackTargetProductId = targetProductId;
        UnpackQuantityPerPackage = quantityPerPackage;

        return Result.Success();
    }

    public void CheckLowStock(int globalThreshold)
    {
        int effectiveThreshold = GetEffectiveThreshold(globalThreshold);

        if (StockQuantity < effectiveThreshold)
        {
            RaiseLowStockEvent(globalThreshold);
        }
    }

    private int GetEffectiveThreshold(int globalThreshold = 0) => LowStockThreshold ?? globalThreshold;

    private void RaiseLowStockEvent(int globalThreshold = 0)
    {
        int effectiveThreshold = GetEffectiveThreshold(globalThreshold);

        AddDomainEvent(new LowStockDetectedEvent(
            Id,
            Name,
            Barcode,
            StockQuantity,
            effectiveThreshold,
            TenantId,
            DateTime.UtcNow));
    }

    private void RaiseStockRecoveredEvent(int globalThreshold = 0)
    {
        int effectiveThreshold = GetEffectiveThreshold(globalThreshold);

        AddDomainEvent(new StockRecoveredEvent(
            Id,
            Name,
            Barcode,
            StockQuantity,
            effectiveThreshold,
            TenantId,
            DateTime.UtcNow));
    }
}
