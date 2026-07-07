namespace Wrapsfer.Application.Abstractions.FileProcessing;

public interface IProductStockReportFileWriter
{
    Task<byte[]> WriteAsync(
        IReadOnlyList<ProductStockReportRow> rows,
        ProductStockReportMetadata metadata,
        ProductStockReportFormat format,
        CancellationToken cancellationToken = default);
}
