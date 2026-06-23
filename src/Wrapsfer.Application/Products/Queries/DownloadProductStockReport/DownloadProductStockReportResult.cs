namespace Wrapsfer.Application.Products.Queries.DownloadProductStockReport;

public sealed record DownloadProductStockReportResult(
    byte[] Content,
    string ContentType,
    string FileName);
