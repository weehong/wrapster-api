using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Products.Responses;

public sealed record ProductResponse(
    Guid Id,
    string TenantId,
    string Barcode,
    string? SkuCode,
    string Name,
    ProductType Type,
    decimal Cost,
    int StockQuantity,
    int? LowStockThreshold,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ProductComponentResponse> Components)
{
    public static ProductResponse FromProduct(
        Product product,
        int? computedStock = null,
        IReadOnlyList<ProductComponentResponse>? components = null) =>
        new(
            product.Id,
            product.TenantId,
            product.Barcode,
            product.SkuCode,
            product.Name,
            product.Type,
            product.Cost,
            computedStock ?? product.StockQuantity,
            product.LowStockThreshold,
            product.CreatedAt,
            product.UpdatedAt,
            components ?? []);
}
