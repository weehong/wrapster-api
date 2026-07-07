using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class CompositeProductFileParser(
    CsvProductFileParser csvParser,
    ExcelProductFileParser excelParser) : IProductFileParser
{
    public Task<ProductImportBatch> ParseAsync(Stream stream, ProductFileFormat format,
        CancellationToken cancellationToken = default) =>
        format switch
        {
            ProductFileFormat.Csv => csvParser.ParseAsync(stream, format, cancellationToken),
            ProductFileFormat.Xlsx => excelParser.ParseAsync(stream, format, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format,
                "Unsupported product file format")
        };
}
