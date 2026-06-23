using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class CompositeProductStockReportFileWriter(
    ExcelProductStockReportFileWriter excelWriter,
    PdfProductStockReportFileWriter pdfWriter) : IProductStockReportFileWriter
{
    public Task<byte[]> WriteAsync(IReadOnlyList<ProductStockReportRow> rows, ProductStockReportMetadata metadata,
        ProductStockReportFormat format, CancellationToken cancellationToken = default) =>
        format switch
        {
            ProductStockReportFormat.Xlsx => excelWriter.WriteAsync(rows, metadata, format, cancellationToken),
            ProductStockReportFormat.Pdf => pdfWriter.WriteAsync(rows, metadata, format, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}
