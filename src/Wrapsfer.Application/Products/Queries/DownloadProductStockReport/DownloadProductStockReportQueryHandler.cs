using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Auditing;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Products.Queries.DownloadProductStockReport;

internal sealed class DownloadProductStockReportQueryHandler(
    IStockMovementRepository stockMovementRepository,
    IProductStockReportFileWriter writer,
    IFeatureEntitlementRepository entitlementRepository,
    ITenantContext tenantContext,
    IAuditMetadata auditMetadata)
    : IQueryHandler<DownloadProductStockReportQuery, DownloadProductStockReportResult>
{
    public async Task<Result<DownloadProductStockReportResult>> Handle(
        DownloadProductStockReportQuery request,
        CancellationToken cancellationToken)
    {
        // Audit metadata is attached even on failure paths so attempts are traceable; only a
        // successful generation sets successfulDownload = true, which is what monthly usage counts.
        auditMetadata.Set("feature", "StockReport");
        auditMetadata.Set("format", request.Format.ToString());
        auditMetadata.Set("asOfDate", request.AsOfDate);
        auditMetadata.Set("successfulDownload", false);

        // Stock report export is gated behind a paid, time-bound access pass.
        FeatureEntitlement? entitlement = await entitlementRepository.GetActiveAsync(
            tenantContext.TenantId, BillingFeature.StockReport, DateTime.UtcNow, cancellationToken);
        if (entitlement is null)
        {
            return Result<DownloadProductStockReportResult>.Failure(BillingErrors.StockReportPaymentRequired);
        }

        // Include the whole of the chosen day: take movements strictly before the next midnight (UTC).
        DateTime asOfUtcExclusive = request.AsOfDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        IReadOnlyList<ProductStockSnapshot> snapshots =
            await stockMovementRepository.GetPointInTimeSnapshotAsync(
                tenantContext.TenantId, asOfUtcExclusive, cancellationToken);

        List<ProductStockReportRow> rows = snapshots
            .Select(s => new ProductStockReportRow(
                s.Barcode,
                s.SkuCode,
                s.Name,
                s.Quantity,
                s.UnitCost,
                s.Quantity * s.UnitCost))
            .ToList();

        ProductStockReportMetadata metadata = new(
            request.AsOfDate,
            tenantContext.DisplayName ?? tenantContext.Username,
            DateTime.UtcNow,
            rows.Count,
            rows.Sum(r => r.Quantity),
            rows.Sum(r => r.TotalValue));

        byte[] fileBytes = await writer.WriteAsync(rows, metadata, request.Format, cancellationToken);
        (string contentType, string extension) = GetFileInfo(request.Format);
        string fileName = $"product-stock-report-{request.AsOfDate:yyyyMMdd}{extension}";

        auditMetadata.Set("fileName", fileName);
        auditMetadata.Set("rowCount", rows.Count);
        auditMetadata.Set("successfulDownload", true);

        return new DownloadProductStockReportResult(fileBytes, contentType, fileName);
    }

    private static (string ContentType, string Extension) GetFileInfo(ProductStockReportFormat format) =>
        format switch
        {
            ProductStockReportFormat.Xlsx =>
                ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"),
            ProductStockReportFormat.Pdf => ("application/pdf", ".pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}
