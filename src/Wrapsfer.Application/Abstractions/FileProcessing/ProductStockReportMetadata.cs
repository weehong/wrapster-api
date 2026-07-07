namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record ProductStockReportMetadata(
    DateOnly AsOfDate,
    string? ExportedBy,
    DateTime GeneratedAtUtc,
    int TotalProducts,
    int TotalUnits,
    decimal GrandTotalValue);
