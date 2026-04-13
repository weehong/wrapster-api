using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;

namespace Wrapster.Application.Tests.Helpers;

internal static class ProductTestFactory
{
    internal static Product CreateSingle(
        string? tenantId = null,
        string? barcode = null,
        string? name = null,
        decimal cost = 10.00m,
        int stockQuantity = 100,
        string? skuCode = null,
        int? lowStockThreshold = null)
    {
        Result<Product> result = Product.Create(
            tenantId ?? "test-tenant",
            barcode ?? $"BC-{Guid.NewGuid():N}",
            name ?? "Test Single Product",
            ProductType.Single,
            cost,
            stockQuantity,
            skuCode,
            lowStockThreshold);

        return result.Value;
    }

    internal static Product CreateBundle(
        string? tenantId = null,
        string? barcode = null,
        string? name = null,
        decimal cost = 25.00m,
        int stockQuantity = 0,
        string? skuCode = null,
        int? lowStockThreshold = null)
    {
        Result<Product> result = Product.Create(
            tenantId ?? "test-tenant",
            barcode ?? $"BC-{Guid.NewGuid():N}",
            name ?? "Test Bundle Product",
            ProductType.Bundle,
            cost,
            stockQuantity,
            skuCode,
            lowStockThreshold);

        return result.Value;
    }

    internal static Product CreatePackage(
        string? tenantId = null,
        string? barcode = null,
        string? name = null,
        decimal cost = 50.00m,
        int stockQuantity = 10,
        string? skuCode = null,
        int? lowStockThreshold = null,
        Guid? unpackTargetProductId = null,
        int? unpackQuantityPerPackage = null)
    {
        Result<Product> result = Product.Create(
            tenantId ?? "test-tenant",
            barcode ?? $"BC-{Guid.NewGuid():N}",
            name ?? "Test Package Product",
            ProductType.Package,
            cost,
            stockQuantity,
            skuCode,
            lowStockThreshold,
            unpackTargetProductId ?? Guid.NewGuid(),
            unpackQuantityPerPackage ?? 6);

        return result.Value;
    }
}
