using Wrapster.Application.Abstractions.FileProcessing;

namespace Wrapster.Application.Products.Responses;

public sealed record ProductImportResult(
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    IReadOnlyList<RowError> Errors);
