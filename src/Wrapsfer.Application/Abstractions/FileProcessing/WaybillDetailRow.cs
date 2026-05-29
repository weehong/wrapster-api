namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record WaybillDetailRow(
    int Index,
    DateOnly Date,
    TimeOnly? Time,
    string WaybillNumber,
    string? Barcode,
    string ProductName);
