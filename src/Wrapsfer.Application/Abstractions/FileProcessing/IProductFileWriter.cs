namespace Wrapsfer.Application.Abstractions.FileProcessing;

public interface IProductFileWriter
{
    Task<byte[]> WriteAsync(IReadOnlyList<ProductExportRow> rows, ProductFileFormat format,
        CancellationToken cancellationToken = default);
}
