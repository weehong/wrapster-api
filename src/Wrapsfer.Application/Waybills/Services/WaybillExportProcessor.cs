using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Application.Waybills.Services;

/// <summary>
/// Generates one waybill report per target tenant, uploads each to private object storage, and
/// emails a time-limited download link. Tenants are processed independently so that one tenant's
/// failure does not block the others.
/// TEMPORARY (dev): the download link is sent to a single inbox tagged per partner
/// (weehongkane+{tenantId}@gmail.com) rather than each tenant's configured recipients.
/// </summary>
public sealed class WaybillExportProcessor(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    IWaybillReportFileWriter writer,
    IReportStorage reportStorage,
    IMailer mailer,
    ILogger<WaybillExportProcessor> logger)
{
    private const int ExportPageSize = 500;

    // TEMPORARY (dev): route every export email to a single inbox, tagged per
    // partner via Gmail subaddressing, instead of each tenant's configured
    // recipients. Revert to ITenantSettingsRepository-based recipients later.
    private const string TemporaryMailboxLocalPart = "weehongkane";
    private const string TemporaryMailboxDomain = "gmail.com";

    public async Task ProcessAsync(WaybillsExportRequestedMessage message, CancellationToken cancellationToken)
    {
        Guid exportId = Guid.NewGuid();

        foreach (string tenantId in message.TenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessTenantAsync(message, tenantId, exportId, cancellationToken);
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
    }

    private async Task ProcessTenantAsync(
        WaybillsExportRequestedMessage message,
        string tenantId,
        Guid exportId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> recipients =
            message.RecipientEmails is { Count: > 0 } explicitRecipients
                ? explicitRecipients
                : [BuildTemporaryRecipient(tenantId)];

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
            $"waybills/{tenantId}/{message.RequestedAt:yyyy}/{message.RequestedAt:MM}/{message.RequestedAt:dd}/{exportId}/{fileName}";

        StoredReport stored = await reportStorage.UploadAsync(objectKey, fileBytes, contentType, cancellationToken);

        MailMessage mail = new()
        {
            To = recipients,
            Subject = "Waybill report export",
            Body = MailBody.FromText(BuildEmailBody(fileName, message, rows.Count, stored))
        };

        MailRequestId requestId = await mailer.SendAsync(mail, cancellationToken);

        logger.LogInformation(
            "Enqueued waybills export mail {MailRequestId} for tenant {TenantId} ({Count} rows, format {Format}) " +
            "to {RecipientCount} recipient(s); object {ObjectKey}",
            requestId, tenantId, rows.Count, message.Format, recipients.Count, stored.ObjectKey);
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

    // TEMPORARY (dev): weehongkane+{tenantId}@gmail.com — see constants above.
    private static string BuildTemporaryRecipient(string tenantId) =>
        $"{TemporaryMailboxLocalPart}+{tenantId}@{TemporaryMailboxDomain}";

    private static string BuildEmailBody(
        string reportName,
        WaybillsExportRequestedMessage message,
        int rowCount,
        StoredReport stored) =>
        $"""
         Your waybill report export is ready.

         Report: {reportName}
         Format: {message.Format}
         Rows: {rowCount}
         Requested (UTC): {message.RequestedAt:yyyy-MM-dd HH:mm}

         Download: {stored.DownloadUrl}
         Link expires (UTC): {stored.ExpiresAtUtc:yyyy-MM-dd HH:mm}
         """;

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
