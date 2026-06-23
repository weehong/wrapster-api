using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Products.Queries.DownloadProductStockReport;

public sealed record DownloadProductStockReportQuery(
    ProductStockReportFormat Format,
    DateOnly AsOfDate) : IQuery<DownloadProductStockReportResult>;
