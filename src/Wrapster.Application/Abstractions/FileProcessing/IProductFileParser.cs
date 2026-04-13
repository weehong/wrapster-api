namespace Wrapster.Application.Abstractions.FileProcessing;

public interface IProductFileParser
{
    Task<ProductImportBatch> ParseAsync(Stream stream, ProductFileFormat format,
        CancellationToken cancellationToken = default);
}
