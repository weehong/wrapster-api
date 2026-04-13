using System.Globalization;
using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;

namespace Wrapster.Infrastructure.FileProcessing;

internal static class ProductExportRowMapper
{
    public static ProductExportRow ToRow(
        Product product,
        IReadOnlyDictionary<Guid, string> barcodeById,
        IReadOnlyList<(Guid ChildId, int Quantity)>? components)
    {
        string? unpackTargetBarcode = product.UnpackTargetProductId.HasValue &&
                                      barcodeById.TryGetValue(product.UnpackTargetProductId.Value, out string? bc)
            ? bc
            : null;

        string? componentsString = null;
        if (product.Type == ProductType.Bundle && components is { Count: > 0 })
        {
            componentsString = string.Join(";", components.Select(c =>
                barcodeById.TryGetValue(c.ChildId, out string? childBarcode)
                    ? $"{childBarcode}:{c.Quantity.ToString(CultureInfo.InvariantCulture)}"
                    : string.Empty).Where(s => !string.IsNullOrEmpty(s)));
            if (string.IsNullOrEmpty(componentsString))
            {
                componentsString = null;
            }
        }

        return new ProductExportRow(
            product.Barcode,
            product.Name,
            product.SkuCode,
            product.Type.ToString(),
            product.Cost,
            product.StockQuantity,
            product.LowStockThreshold,
            unpackTargetBarcode,
            product.UnpackQuantityPerPackage,
            componentsString);
    }
}
