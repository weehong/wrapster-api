namespace Wrapster.Application.Abstractions.FileProcessing;

public sealed record ProductImportBatch(
    IReadOnlyList<ProductImportRow> Rows,
    IReadOnlyList<RowError> ParseErrors);
