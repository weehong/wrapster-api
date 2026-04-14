namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record ProductExportRow(
    string Barcode,
    string Name,
    string? SkuCode,
    string Type,
    decimal Cost,
    int StockQuantity,
    int? LowStockThreshold,
    string? UnpackTargetBarcode,
    int? UnpackQuantityPerPackage,
    string? Components);
