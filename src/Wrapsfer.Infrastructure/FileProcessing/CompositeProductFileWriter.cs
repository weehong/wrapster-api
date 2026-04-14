using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class CompositeProductFileWriter(
    CsvProductFileWriter csvWriter,
    ExcelProductFileWriter excelWriter) : IProductFileWriter
{
    public Task<byte[]> WriteAsync(IReadOnlyList<ProductExportRow> rows, ProductFileFormat format,
        CancellationToken cancellationToken = default) =>
        format switch
        {
            ProductFileFormat.Csv => csvWriter.WriteAsync(rows, format, cancellationToken),
            ProductFileFormat.Xlsx => excelWriter.WriteAsync(rows, format, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format,
                "Unsupported product file format")
        };
}
