namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record ProductImportBatch(
    IReadOnlyList<ProductImportRow> Rows,
    IReadOnlyList<RowError> ParseErrors);
