namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record RowError(int RowNumber, string? Column, string? Barcode, string Message);
