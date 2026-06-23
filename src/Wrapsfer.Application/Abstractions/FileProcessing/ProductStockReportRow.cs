namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record ProductStockReportRow(
    string Barcode,
    string? SkuCode,
    string Name,
    int Quantity,
    decimal UnitCost,
    decimal TotalValue);
