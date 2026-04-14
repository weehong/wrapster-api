using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Tests.Helpers;

namespace Wrapsfer.Domain.Tests.Entities;

public class ProductTests
{
    // --- Create factory ---

    [Fact]
    public void Create_WithValidSingleProduct_ReturnsSuccess()
    {
        Result<Product> result = Product.Create("tenant", "BC-001", "Widget", ProductType.Single, 9.99m, 50);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(ProductType.Single);
    }

    [Fact]
    public void Create_WithValidBundleProduct_ReturnsSuccess()
    {
        Result<Product> result = Product.Create("tenant", "BC-002", "Bundle", ProductType.Bundle, 20m, 0);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(ProductType.Bundle);
    }

    [Fact]
    public void Create_WithValidPackageProduct_ReturnsSuccess()
    {
        Guid targetId = Guid.NewGuid();
        Result<Product> result = Product.Create(
            "tenant", "BC-003", "Package", ProductType.Package, 50m, 10,
            unpackTargetProductId: targetId, unpackQuantityPerPackage: 6);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(ProductType.Package);
        result.Value.UnpackTargetProductId.Should().Be(targetId);
        result.Value.UnpackQuantityPerPackage.Should().Be(6);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ReturnsFailure()
    {
        Result<Product> result = Product.Create("", "BC-001", "Widget", ProductType.Single, 9.99m, 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidTenantId.Code);
    }

    [Fact]
    public void Create_WithWhitespaceTenantId_ReturnsFailure()
    {
        Result<Product> result = Product.Create("   ", "BC-001", "Widget", ProductType.Single, 9.99m, 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidTenantId.Code);
    }

    [Fact]
    public void Create_WithEmptyBarcode_ReturnsFailure()
    {
        Result<Product> result = Product.Create("tenant", "", "Widget", ProductType.Single, 9.99m, 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidBarcode.Code);
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsFailure()
    {
        Result<Product> result = Product.Create("tenant", "BC-001", "", ProductType.Single, 9.99m, 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidName.Code);
    }

    [Fact]
    public void Create_WithNegativeCost_ReturnsFailure()
    {
        Result<Product> result = Product.Create("tenant", "BC-001", "Widget", ProductType.Single, -5m, 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidCost.Code);
    }

    [Fact]
    public void Create_WithNegativeStockQuantity_ReturnsFailure()
    {
        Result<Product> result = Product.Create("tenant", "BC-001", "Widget", ProductType.Single, 10m, -1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidStockQuantity.Code);
    }

    [Fact]
    public void Create_WithPackageType_MissingUnpackTarget_ReturnsFailure()
    {
        Result<Product> result = Product.Create(
            "tenant", "BC-003", "Package", ProductType.Package, 50m, 10);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.MissingUnpackTarget.Code);
    }

    [Fact]
    public void Create_WithPackageType_MissingUnpackQuantity_ReturnsFailure()
    {
        Result<Product> result = Product.Create(
            "tenant", "BC-003", "Package", ProductType.Package, 50m, 10,
            unpackTargetProductId: Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.MissingUnpackTarget.Code);
    }

    [Fact]
    public void Create_SetsAllPropertiesCorrectly()
    {
        Result<Product> result = Product.Create(
            "tenant-1", "BC-100", "Full Product", ProductType.Single, 15.50m, 200, "SKU-100", 10);

        Product product = result.Value;
        product.TenantId.Should().Be("tenant-1");
        product.Barcode.Should().Be("BC-100");
        product.Name.Should().Be("Full Product");
        product.Type.Should().Be(ProductType.Single);
        product.Cost.Should().Be(15.50m);
        product.StockQuantity.Should().Be(200);
        product.SkuCode.Should().Be("SKU-100");
        product.LowStockThreshold.Should().Be(10);
    }

    // --- DeductStock ---

    [Fact]
    public void DeductStock_WhenBundleType_ReturnsCannotDeductBundleStockFailure()
    {
        Product bundle = ProductFactory.CreateBundle();

        Result result = bundle.DeductStock(1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.CannotDeductBundleStock.Code);
    }

    [Fact]
    public void DeductStock_WhenAmountExceedsStock_ReturnsInsufficientStockFailure()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 5);

        Result result = product.DeductStock(10);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InsufficientStock.Code);
    }

    [Fact]
    public void DeductStock_WhenValid_DeductsAndReturnsSuccess()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 50);

        Result result = product.DeductStock(20);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(30);
    }

    // --- Unpack ---

    [Fact]
    public void Unpack_WhenNotPackageType_ReturnsNotAPackageFailure()
    {
        Product single = ProductFactory.CreateSingle();

        Result<int> result = single.Unpack(1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotAPackage.Code);
    }

    [Fact]
    public void Unpack_WhenQuantityExceedsStock_ReturnsInsufficientStockFailure()
    {
        Product package = ProductFactory.CreatePackage(stockQuantity: 2);

        Result<int> result = package.Unpack(5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InsufficientStock.Code);
    }

    [Fact]
    public void Unpack_WhenValid_DeductsStockAndReturnsRestockUnits()
    {
        Product package = ProductFactory.CreatePackage(stockQuantity: 10, unpackQuantityPerPackage: 6);

        Result<int> result = package.Unpack(3);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(18); // 3 * 6
        package.StockQuantity.Should().Be(7); // 10 - 3
    }

    // --- ComputeBundleQuantity ---

    [Fact]
    public void ComputeBundleQuantity_WithEmptyList_ReturnsZero()
    {
        int result = Product.ComputeBundleQuantity([]);

        result.Should().Be(0);
    }

    [Fact]
    public void ComputeBundleQuantity_WithSingleComponent_ReturnsFloorOfStockDividedByRatio()
    {
        List<(int stock, int ratio)> data = [(10, 3)];

        int result = Product.ComputeBundleQuantity(data);

        result.Should().Be(3); // floor(10/3)
    }

    [Fact]
    public void ComputeBundleQuantity_WithMultipleComponents_ReturnsMinimumFloor()
    {
        List<(int stock, int ratio)> data = [(50, 2), (30, 3), (100, 5)];

        int result = Product.ComputeBundleQuantity(data);

        result.Should().Be(10); // min(25, 10, 20) = 10
    }

    [Fact]
    public void ComputeBundleQuantity_WithZeroRatio_ReturnsZero()
    {
        List<(int stock, int ratio)> data = [(10, 0)];

        int result = Product.ComputeBundleQuantity(data);

        result.Should().Be(0);
    }

    // --- RestoreStock ---

    [Fact]
    public void RestoreStock_WhenValid_IncreasesStockQuantity()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);

        Result result = product.RestoreStock(5);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(15);
    }

    [Fact]
    public void RestoreStock_WhenNegativeAmount_ReturnsFailure()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);

        Result result = product.RestoreStock(-5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidRestoreAmount.Code);
    }

    [Fact]
    public void RestoreStock_WhenZeroAmount_ReturnsFailure()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);

        Result result = product.RestoreStock(0);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidRestoreAmount.Code);
    }

    [Fact]
    public void RestoreStock_WhenBundleType_ReturnsFailure()
    {
        Product bundle = ProductFactory.CreateBundle();

        Result result = bundle.RestoreStock(5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.CannotSetBundleStock.Code);
    }

    // --- SetStockQuantity ---

    [Fact]
    public void SetStockQuantity_WhenValid_SetsValue()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);

        Result result = product.SetStockQuantity(99);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(99);
    }

    [Fact]
    public void SetStockQuantity_WhenNegative_ReturnsFailure()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 10);

        Result result = product.SetStockQuantity(-1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidStockQuantity.Code);
    }

    [Fact]
    public void SetStockQuantity_WhenBundleType_ReturnsFailure()
    {
        Product bundle = ProductFactory.CreateBundle();

        Result result = bundle.SetStockQuantity(10);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.CannotSetBundleStock.Code);
    }

    // --- Update ---

    [Fact]
    public void Update_OnlyUpdatesNonNullFields()
    {
        Product product = ProductFactory.CreateSingle(name: "Original", cost: 10m);

        product.Update("Updated", null, false, 20m, null, false);

        product.Name.Should().Be("Updated");
        product.Cost.Should().Be(20m);
    }

    [Fact]
    public void Update_ClearSkuCode_SetsSkuToNull()
    {
        Product product = ProductFactory.CreateSingle(skuCode: "SKU-001");

        product.Update(null, null, true, null, null, false);

        product.SkuCode.Should().BeNull();
    }

    [Fact]
    public void Update_ClearLowStockThreshold_SetsThresholdToNull()
    {
        Product product = ProductFactory.CreateSingle(lowStockThreshold: 10);

        product.Update(null, null, false, null, null, true);

        product.LowStockThreshold.Should().BeNull();
    }

    // --- UpdateUnpackConfig ---

    [Fact]
    public void UpdateUnpackConfig_WhenPackage_SetsBothFields()
    {
        Product package = ProductFactory.CreatePackage();
        Guid newTargetId = Guid.NewGuid();

        Result result = package.UpdateUnpackConfig(newTargetId, 12);

        result.IsSuccess.Should().BeTrue();
        package.UnpackTargetProductId.Should().Be(newTargetId);
        package.UnpackQuantityPerPackage.Should().Be(12);
    }

    [Fact]
    public void UpdateUnpackConfig_WhenNotPackage_ReturnsFailure()
    {
        Product single = ProductFactory.CreateSingle();
        Guid newTargetId = Guid.NewGuid();

        Result result = single.UpdateUnpackConfig(newTargetId, 12);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotAPackageForUnpackConfig.Code);
    }

    [Fact]
    public void UpdateUnpackConfig_WhenSelfReferencing_ReturnsFailure()
    {
        Product package = ProductFactory.CreatePackage();

        Result result = package.UpdateUnpackConfig(package.Id, 12);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.SelfReferencingUnpackTarget.Code);
    }

    // --- CheckLowStock ---

    [Fact]
    public void CheckLowStock_WhenStockBelowProductThreshold_RaisesEvent()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 3, lowStockThreshold: 5);

        product.CheckLowStock(10);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LowStockDetectedEvent>();
    }

    [Fact]
    public void CheckLowStock_WhenStockBelowGlobalThreshold_RaisesEvent()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 3);

        product.CheckLowStock(5);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LowStockDetectedEvent>();
    }

    [Fact]
    public void CheckLowStock_WhenStockAboveThreshold_DoesNotRaiseEvent()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 50);

        product.CheckLowStock(5);

        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetStockQuantity_WhenCrossingThreshold_RaisesLowStockEvent()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 20, lowStockThreshold: 10);

        product.SetStockQuantity(5);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LowStockDetectedEvent>();
    }

    [Fact]
    public void SetStockQuantity_WhenAlreadyBelowThreshold_DoesNotRaiseEvent()
    {
        Product product = ProductFactory.CreateSingle(stockQuantity: 3, lowStockThreshold: 10);

        product.SetStockQuantity(2);

        product.DomainEvents.Should().BeEmpty();
    }
}
