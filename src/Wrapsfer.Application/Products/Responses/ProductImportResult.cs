using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Application.Products.Responses;

public sealed record ProductImportResult(
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    IReadOnlyList<RowError> Errors);
