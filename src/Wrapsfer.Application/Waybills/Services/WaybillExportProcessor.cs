using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Services;

/// <summary>
/// Generates one waybill report per target tenant and uploads each to private object storage.
/// Tenants are processed independently so that one tenant's failure does not block the others.
/// Email delivery is the responsibility of the synchronous EmailWaybillsReport command — the
/// background path here is upload-only.
/// </summary>
public sealed class WaybillExportProcessor(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    IWaybillReportFileWriter writer,
    IReportStorage reportStorage,
    ILogger<WaybillExportProcessor> logger)
{
    private const int ExportPageSize = 500;

    public async Task<IReadOnlyList<string>> ProcessAsync(
        WaybillsExportRequestedMessage message, CancellationToken cancellationToken)
    {
        List<string> uploadedObjectKeys = new();

        foreach (string tenantId in message.TenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string? objectKey = await ProcessTenantAsync(message, tenantId, cancellationToken);
                if (!string.IsNullOrEmpty(objectKey))
                {
                    uploadedObjectKeys.Add(objectKey);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to process waybill export for tenant {TenantId}; continuing with remaining tenants",
                    tenantId);
            }
        }

        return uploadedObjectKeys;
    }

    private async Task<string?> ProcessTenantAsync(
        WaybillsExportRequestedMessage message,
        string tenantId,
        CancellationToken cancellationToken)
    {
        List<Waybill> waybills = await LoadWaybillsAsync(message, tenantId, cancellationToken);

        HashSet<Guid> productIds = waybills
            .SelectMany(w => w.Items)
            .Select(i => i.ProductId)
            .ToHashSet();

        IReadOnlyDictionary<Guid, Product> productsById = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : (await productRepository.GetByIdsAsync(productIds, tenantId, cancellationToken))
            .ToDictionary(p => p.Id);

        List<WaybillReportRow> rows = waybills.SelectMany(w => ToRows(w, productsById)).ToList();

        byte[] fileBytes = await writer.WriteAsync(rows, message.Format, cancellationToken);
        (string contentType, string extension) = GetFileInfo(message.Format);
        string fileName = $"waybills-report-{message.RequestedAt:yyyyMMdd-HHmmss}{extension}";

        string objectKey =
            $"waybills/{tenantId}/{message.RequestedAt:yyyy-MM-dd}/{fileName}";

        StoredReport stored = await reportStorage.UploadAsync(objectKey, fileBytes, contentType, cancellationToken);

        logger.LogInformation(
            "Uploaded waybills export for tenant {TenantId} ({Count} rows, format {Format}); object {ObjectKey}",
            tenantId, rows.Count, message.Format, stored.ObjectKey);

        return stored.ObjectKey;
    }

    private async Task<List<Waybill>> LoadWaybillsAsync(
        WaybillsExportRequestedMessage message,
        string tenantId,
        CancellationToken cancellationToken)
    {
        string[] singleTenant = [tenantId];
        List<Waybill> all = [];
        int page = 1;
        int totalCount;
        do
        {
            (IReadOnlyList<Waybill> items, int total) = await waybillRepository.ListByTenantIdsAsync(
                singleTenant,
                message.From,
                message.To,
                message.Status,
                message.Search,
                page,
                ExportPageSize,
                cancellationToken);
            all.AddRange(items);
            totalCount = total;
            page++;
        } while (all.Count < totalCount);

        return all;
    }

    private static IEnumerable<WaybillReportRow> ToRows(Waybill waybill,
        IReadOnlyDictionary<Guid, Product> productsById)
    {
        if (waybill.Items.Count == 0)
        {
            return [ToRow(waybill, null, null)];
        }

        return waybill.Items.Select(item =>
        {
            productsById.TryGetValue(item.ProductId, out Product? product);
            return ToRow(waybill, item, product);
        });
    }

    private static WaybillReportRow ToRow(Waybill waybill, WaybillItem? item, Product? product) => new(
        waybill.TenantId,
        waybill.PackagingDate,
        waybill.WaybillNumber,
        waybill.Status,
        waybill.CancellationReason,
        waybill.PackedAt,
        waybill.HandedOffAt,
        waybill.CancelledAt,
        waybill.CreatedAt,
        waybill.CreatedBy,
        product?.Barcode,
        product?.Name,
        item?.Quantity);

    private static (string ContentType, string Extension) GetFileInfo(WaybillExportFormat format) =>
        format switch
        {
            WaybillExportFormat.Csv => ("text/csv", ".csv"),
            WaybillExportFormat.Xlsx =>
                ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"),
            WaybillExportFormat.Pdf => ("application/pdf", ".pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}
