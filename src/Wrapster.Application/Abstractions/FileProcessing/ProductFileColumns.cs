namespace Wrapster.Application.Abstractions.FileProcessing;

public static class ProductFileColumns
{
    public const string Barcode = "barcode";
    public const string Name = "name";
    public const string SkuCode = "skuCode";
    public const string Type = "type";
    public const string Cost = "cost";
    public const string StockQuantity = "stockQuantity";
    public const string LowStockThreshold = "lowStockThreshold";
    public const string UnpackTargetBarcode = "unpackTargetBarcode";
    public const string UnpackQuantityPerPackage = "unpackQuantityPerPackage";
    public const string Components = "components";

    public static readonly IReadOnlyList<string> AllInOrder =
    [
        Barcode, Name, SkuCode, Type, Cost, StockQuantity,
        LowStockThreshold, UnpackTargetBarcode, UnpackQuantityPerPackage, Components
    ];
}
