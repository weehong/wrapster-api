namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record WaybillProductQuantity(
    int Rank,
    string ProductName,
    string? Barcode,
    int TotalQuantity);
