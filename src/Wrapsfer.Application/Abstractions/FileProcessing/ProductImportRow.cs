namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record ProductImportRow(
    int RowNumber,
    string? Barcode,
    string? Name,
    string? SkuCode,
    string? Type,
    string? Cost,
    string? StockQuantity,
    string? LowStockThreshold,
    string? UnpackTargetBarcode,
    string? UnpackQuantityPerPackage,
    string? Components);
